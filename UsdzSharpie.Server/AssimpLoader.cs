using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.Extensions.DependencyModel;
using OpenTK.Mathematics;
using Silk.NET.Assimp;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Loader;

namespace UsdzSharpie.Server
{
    public class AssimpLoader : IDisposable
    {
        private readonly Assimp assimp;
        private string? tempDirectory;

        public AssimpLoader()
        {
            if (OperatingSystem.IsLinux())
            {
                var rid = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
                var libraryName = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "libassimp.so.5");
                if (System.IO.File.Exists(libraryName))
                {
                    assimp = new Assimp(new DefaultNativeContext(libraryName));
                    Console.WriteLine($"Loaded Assimp library from: {libraryName}");
                    Console.WriteLine($"Assimp version: {assimp.GetVersionMajor()}.{assimp.GetVersionMinor()}.{assimp.GetVersionRevision()}");
                    return;
                }
            }

            assimp = Assimp.GetApi();
        }

        public AssimpScene LoadUsdz(string usdzPath)
        {
            // USDZ is a ZIP archive, extract it to a temp directory
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);

            try
            {
                // Extract USDZ
                ZipFile.ExtractToDirectory(usdzPath, tempDirectory);

                // Find the main USD file (usually the first .usdc or .usda file)
                var usdFiles = Directory.GetFiles(tempDirectory, "*.usd*", SearchOption.AllDirectories);
                if (usdFiles.Length == 0)
                {
                    throw new Exception("No USD files found in USDZ archive");
                }

                var mainUsdFile = usdFiles[0];
                Console.WriteLine($"Loading USD file: {mainUsdFile}");

                // Load with Assimp
                unsafe
                {
                    var scene = assimp.ImportFile(mainUsdFile, (uint)(PostProcessSteps.Triangulate |
                                                                        PostProcessSteps.GenerateNormals |
                                                                        PostProcessSteps.CalculateTangentSpace |
                                                                        PostProcessSteps.JoinIdenticalVertices |
                                                                        PostProcessSteps.FlipUVs));

                    if (scene == null || scene->MRootNode == null)
                    {
                        var error = assimp.GetErrorStringS();
                        throw new Exception($"Failed to load USD file with Assimp: {error}");
                    }

                    return ParseScene(scene, tempDirectory);
                }
            }
            catch (Exception ex)
            {
                // Cleanup temp directory if loading failed
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                    tempDirectory = null;
                }
                throw new Exception($"Failed to load USDZ file: {ex.Message}", ex);
            }
        }

        private unsafe AssimpScene ParseScene(Silk.NET.Assimp.Scene* scene, string baseDirectory)
        {
            var result = new AssimpScene();

            // Parse materials
            for (uint i = 0; i < scene->MNumMaterials; i++)
            {
                var material = scene->MMaterials[i];
                var parsedMaterial = ParseMaterial(material, baseDirectory);
                result.Materials.Add(parsedMaterial);
            }

            // Parse meshes
            for (uint i = 0; i < scene->MNumMeshes; i++)
            {
                var mesh = scene->MMeshes[i];
                var parsedMesh = ParseMesh(mesh, i < result.Materials.Count ? result.Materials[(int)i] : null);
                result.Meshes.Add(parsedMesh);
            }

            // Parse scene graph
            result.RootNode = ParseNode(scene->MRootNode, scene);

            return result;
        }

        private unsafe AssimpMaterial ParseMaterial(Silk.NET.Assimp.Material* material, string baseDirectory)
        {
            var result = new AssimpMaterial();

            // Get diffuse color
            var diffuseColor = new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            assimp.GetMaterialColor(material, Assimp.MatkeyColorDiffuse, 0, 0, ref diffuseColor);
            result.DiffuseColor = new Vector3(diffuseColor.X, diffuseColor.Y, diffuseColor.Z);

            // Get diffuse texture
            AssimpString path = new AssimpString();
            if (assimp.GetMaterialTexture(material, TextureType.Diffuse, 0, &path, null, null, null, null, null, null) == Return.Success)
            {
                var texturePath = path.AsString;
                if (!string.IsNullOrEmpty(texturePath))
                {
                    // Convert to absolute path
                    var fullPath = Path.Combine(baseDirectory, texturePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        result.DiffuseTexturePath = fullPath;
                        result.DiffuseTextureData = System.IO.File.ReadAllBytes(fullPath);
                    }
                }
            }

            return result;
        }

        private unsafe AssimpMesh ParseMesh(Silk.NET.Assimp.Mesh* mesh, AssimpMaterial? material)
        {
            var result = new AssimpMesh
            {
                Name = mesh->MName.AsString,
                Material = material
            };

            // Get vertices
            var vertices = new List<Vector3>();
            for (uint i = 0; i < mesh->MNumVertices; i++)
            {
                var v = mesh->MVertices[i];
                vertices.Add(new Vector3(v.X, v.Y, v.Z));
            }
            result.Vertices = vertices.ToArray();

            // Get normals
            if (mesh->MNormals != null)
            {
                var normals = new List<Vector3>();
                for (uint i = 0; i < mesh->MNumVertices; i++)
                {
                    var n = mesh->MNormals[i];
                    normals.Add(new Vector3(n.X, n.Y, n.Z));
                }
                result.Normals = normals.ToArray();
            }

            // Get texture coordinates
            if (mesh->MTextureCoords[0] != null)
            {
                var texCoords = new List<Vector2>();
                for (uint i = 0; i < mesh->MNumVertices; i++)
                {
                    var tc = mesh->MTextureCoords[0][i];
                    texCoords.Add(new Vector2(tc.X, tc.Y));
                }
                result.TexCoords = texCoords.ToArray();
            }

            // Get indices
            var indices = new List<uint>();
            for (uint i = 0; i < mesh->MNumFaces; i++)
            {
                var face = mesh->MFaces[i];
                for (uint j = 0; j < face.MNumIndices; j++)
                {
                    indices.Add(face.MIndices[j]);
                }
            }
            result.Indices = indices.ToArray();

            return result;
        }

        private unsafe AssimpNode ParseNode(Silk.NET.Assimp.Node* node, Silk.NET.Assimp.Scene* scene)
        {
            var result = new AssimpNode
            {
                Name = node->MName.AsString
            };

            // Get transformation matrix
            var transform = node->MTransformation;
            result.Transform = new Matrix4(
                transform.M11, transform.M21, transform.M31, transform.M41,
                transform.M12, transform.M22, transform.M32, transform.M42,
                transform.M13, transform.M23, transform.M33, transform.M43,
                transform.M14, transform.M24, transform.M34, transform.M44
            );

            // Get mesh indices
            for (uint i = 0; i < node->MNumMeshes; i++)
            {
                result.MeshIndices.Add((int)node->MMeshes[i]);
            }

            // Parse children
            for (uint i = 0; i < node->MNumChildren; i++)
            {
                var child = ParseNode(node->MChildren[i], scene);
                result.Children.Add(child);
            }

            return result;
        }

        public void Dispose()
        {
            // Cleanup temp directory
            if (tempDirectory != null && Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete temp directory: {ex.Message}");
                }
                tempDirectory = null;
            }

            assimp.Dispose();
        }
    }

    public class AssimpScene
    {
        public List<AssimpMesh> Meshes { get; set; } = new List<AssimpMesh>();
        public List<AssimpMaterial> Materials { get; set; } = new List<AssimpMaterial>();
        public AssimpNode? RootNode { get; set; }

        public IEnumerable<(AssimpMesh Mesh, Matrix4 Transform)> GetAllMeshes()
        {
            if (RootNode == null)
                yield break;

            foreach (var item in GetMeshesRecursive(RootNode, Matrix4.Identity))
            {
                yield return item;
            }
        }

        private IEnumerable<(AssimpMesh Mesh, Matrix4 Transform)> GetMeshesRecursive(AssimpNode node, Matrix4 parentTransform)
        {
            var worldTransform = node.Transform * parentTransform;

            foreach (var meshIndex in node.MeshIndices)
            {
                if (meshIndex >= 0 && meshIndex < Meshes.Count)
                {
                    yield return (Meshes[meshIndex], worldTransform);
                }
            }

            foreach (var child in node.Children)
            {
                foreach (var item in GetMeshesRecursive(child, worldTransform))
                {
                    yield return item;
                }
            }
        }
    }

    public class AssimpNode
    {
        public string Name { get; set; } = "";
        public Matrix4 Transform { get; set; } = Matrix4.Identity;
        public List<int> MeshIndices { get; set; } = new List<int>();
        public List<AssimpNode> Children { get; set; } = new List<AssimpNode>();
    }

    public class AssimpMesh
    {
        public string Name { get; set; } = "";
        public Vector3[] Vertices { get; set; } = Array.Empty<Vector3>();
        public Vector3[] Normals { get; set; } = Array.Empty<Vector3>();
        public Vector2[] TexCoords { get; set; } = Array.Empty<Vector2>();
        public uint[] Indices { get; set; } = Array.Empty<uint>();
        public AssimpMaterial? Material { get; set; }
    }

    public class AssimpMaterial
    {
        public Vector3 DiffuseColor { get; set; } = new Vector3(1.0f, 1.0f, 1.0f);
        public string? DiffuseTexturePath { get; set; }
        public byte[]? DiffuseTextureData { get; set; }
    }
}
