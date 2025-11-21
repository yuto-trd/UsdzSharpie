using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;

namespace UsdzSharpie.Server
{
    public class GltfExporter
    {
        public static byte[] ExportToZip(AssimpScene scene, string modelName = "model")
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                // Generate binary buffer
                var bufferData = GenerateBufferData(scene, out var meshInfos);
                var bufferEntry = archive.CreateEntry($"{modelName}.bin");
                using (var stream = bufferEntry.Open())
                {
                    stream.Write(bufferData, 0, bufferData.Length);
                }

                // Process textures
                var imageInfos = ProcessTextures(scene, archive);

                // Generate glTF JSON
                var gltfJson = GenerateGltfJson(scene, modelName, bufferData.Length, meshInfos, imageInfos);
                var gltfEntry = archive.CreateEntry($"{modelName}.gltf");
                using (var writer = new StreamWriter(gltfEntry.Open()))
                {
                    writer.Write(gltfJson);
                }
            }

            return memoryStream.ToArray();
        }

        private static byte[] GenerateBufferData(AssimpScene scene, out List<MeshInfo> meshInfos)
        {
            using var bufferStream = new MemoryStream();
            using var writer = new BinaryWriter(bufferStream);

            meshInfos = new List<MeshInfo>();
            var meshes = scene.GetAllMeshes().ToList();

            foreach (var (mesh, transform) in meshes)
            {
                var meshInfo = new MeshInfo();

                // Write vertices
                meshInfo.PositionOffset = (int)bufferStream.Position;
                foreach (var vertex in mesh.Vertices)
                {
                    var transformed = Vector3.TransformPosition(vertex, transform);
                    writer.Write(transformed.X);
                    writer.Write(transformed.Y);
                    writer.Write(transformed.Z);
                }
                meshInfo.PositionCount = mesh.Vertices.Length;

                // Align to 4 bytes
                while (bufferStream.Position % 4 != 0)
                    writer.Write((byte)0);

                // Write normals
                if (mesh.Normals.Length > 0)
                {
                    meshInfo.NormalOffset = (int)bufferStream.Position;
                    var normalMatrix = transform.ClearTranslation();
                    foreach (var normal in mesh.Normals)
                    {
                        var transformed = Vector3.TransformNormal(normal, normalMatrix);
                        transformed = Vector3.Normalize(transformed);
                        writer.Write(transformed.X);
                        writer.Write(transformed.Y);
                        writer.Write(transformed.Z);
                    }
                    meshInfo.NormalCount = mesh.Normals.Length;

                    // Align to 4 bytes
                    while (bufferStream.Position % 4 != 0)
                        writer.Write((byte)0);
                }

                // Write texture coordinates
                if (mesh.TexCoords.Length > 0)
                {
                    meshInfo.TexCoordOffset = (int)bufferStream.Position;
                    foreach (var texCoord in mesh.TexCoords)
                    {
                        writer.Write(texCoord.X);
                        writer.Write(texCoord.Y);
                    }
                    meshInfo.TexCoordCount = mesh.TexCoords.Length;

                    // Align to 4 bytes
                    while (bufferStream.Position % 4 != 0)
                        writer.Write((byte)0);
                }

                // Write indices
                meshInfo.IndexOffset = (int)bufferStream.Position;
                foreach (var index in mesh.Indices)
                {
                    writer.Write((ushort)index);
                }
                meshInfo.IndexCount = mesh.Indices.Length;

                // Align to 4 bytes
                while (bufferStream.Position % 4 != 0)
                    writer.Write((byte)0);

                // Calculate bounding box
                meshInfo.MinPosition = new Vector3(float.MaxValue);
                meshInfo.MaxPosition = new Vector3(float.MinValue);
                foreach (var vertex in mesh.Vertices)
                {
                    var transformed = Vector3.TransformPosition(vertex, transform);
                    meshInfo.MinPosition = Vector3.ComponentMin(meshInfo.MinPosition, transformed);
                    meshInfo.MaxPosition = Vector3.ComponentMax(meshInfo.MaxPosition, transformed);
                }

                // Store material index
                meshInfo.MaterialIndex = scene.Meshes.IndexOf(mesh);

                meshInfos.Add(meshInfo);
            }

            return bufferStream.ToArray();
        }

        private static List<ImageInfo> ProcessTextures(AssimpScene scene, ZipArchive archive)
        {
            var imageInfos = new List<ImageInfo>();
            var textureMap = new Dictionary<string, int>();

            for (int i = 0; i < scene.Materials.Count; i++)
            {
                var material = scene.Materials[i];
                if (material.DiffuseTextureData != null && material.DiffuseTexturePath != null)
                {
                    if (!textureMap.ContainsKey(material.DiffuseTexturePath))
                    {
                        var extension = Path.GetExtension(material.DiffuseTexturePath).ToLowerInvariant();
                        var mimeType = extension switch
                        {
                            ".png" => "image/png",
                            ".jpg" or ".jpeg" => "image/jpeg",
                            _ => "image/png"
                        };

                        var imageIndex = imageInfos.Count;
                        var imageName = $"texture_{imageIndex}{extension}";

                        var textureEntry = archive.CreateEntry(imageName);
                        using (var stream = textureEntry.Open())
                        {
                            stream.Write(material.DiffuseTextureData, 0, material.DiffuseTextureData.Length);
                        }

                        imageInfos.Add(new ImageInfo
                        {
                            Uri = imageName,
                            MimeType = mimeType
                        });

                        textureMap[material.DiffuseTexturePath] = imageIndex;
                    }
                }
            }

            return imageInfos;
        }

        private static string GenerateGltfJson(AssimpScene scene, string modelName, int bufferLength,
            List<MeshInfo> meshInfos, List<ImageInfo> imageInfos)
        {
            var gltf = new GltfRoot
            {
                Asset = new GltfAsset
                {
                    Version = "2.0",
                    Generator = "UsdzSharpie USDZ to glTF Converter"
                }
            };

            // Buffers
            gltf.Buffers.Add(new GltfBuffer
            {
                Uri = $"{modelName}.bin",
                ByteLength = bufferLength
            });

            // BufferViews and Accessors
            int accessorIndex = 0;
            var meshGltfInfos = new List<MeshGltfInfo>();

            foreach (var meshInfo in meshInfos)
            {
                var meshGltfInfo = new MeshGltfInfo();

                // Position buffer view and accessor
                var positionBufferView = gltf.BufferViews.Count;
                gltf.BufferViews.Add(new GltfBufferView
                {
                    Buffer = 0,
                    ByteOffset = meshInfo.PositionOffset,
                    ByteLength = meshInfo.PositionCount * 12, // 3 floats * 4 bytes
                    Target = 34962 // ARRAY_BUFFER
                });

                meshGltfInfo.PositionAccessor = accessorIndex++;
                gltf.Accessors.Add(new GltfAccessor
                {
                    BufferView = positionBufferView,
                    ComponentType = 5126, // FLOAT
                    Count = meshInfo.PositionCount,
                    Type = "VEC3",
                    Min = new[] { meshInfo.MinPosition.X, meshInfo.MinPosition.Y, meshInfo.MinPosition.Z },
                    Max = new[] { meshInfo.MaxPosition.X, meshInfo.MaxPosition.Y, meshInfo.MaxPosition.Z }
                });

                // Normal buffer view and accessor
                if (meshInfo.NormalCount > 0)
                {
                    var normalBufferView = gltf.BufferViews.Count;
                    gltf.BufferViews.Add(new GltfBufferView
                    {
                        Buffer = 0,
                        ByteOffset = meshInfo.NormalOffset,
                        ByteLength = meshInfo.NormalCount * 12,
                        Target = 34962
                    });

                    meshGltfInfo.NormalAccessor = accessorIndex++;
                    gltf.Accessors.Add(new GltfAccessor
                    {
                        BufferView = normalBufferView,
                        ComponentType = 5126,
                        Count = meshInfo.NormalCount,
                        Type = "VEC3"
                    });
                }

                // TexCoord buffer view and accessor
                if (meshInfo.TexCoordCount > 0)
                {
                    var texCoordBufferView = gltf.BufferViews.Count;
                    gltf.BufferViews.Add(new GltfBufferView
                    {
                        Buffer = 0,
                        ByteOffset = meshInfo.TexCoordOffset,
                        ByteLength = meshInfo.TexCoordCount * 8, // 2 floats * 4 bytes
                        Target = 34962
                    });

                    meshGltfInfo.TexCoordAccessor = accessorIndex++;
                    gltf.Accessors.Add(new GltfAccessor
                    {
                        BufferView = texCoordBufferView,
                        ComponentType = 5126,
                        Count = meshInfo.TexCoordCount,
                        Type = "VEC2"
                    });
                }

                // Index buffer view and accessor
                var indexBufferView = gltf.BufferViews.Count;
                gltf.BufferViews.Add(new GltfBufferView
                {
                    Buffer = 0,
                    ByteOffset = meshInfo.IndexOffset,
                    ByteLength = meshInfo.IndexCount * 2, // ushort
                    Target = 34963 // ELEMENT_ARRAY_BUFFER
                });

                meshGltfInfo.IndexAccessor = accessorIndex++;
                gltf.Accessors.Add(new GltfAccessor
                {
                    BufferView = indexBufferView,
                    ComponentType = 5123, // UNSIGNED_SHORT
                    Count = meshInfo.IndexCount,
                    Type = "SCALAR"
                });

                meshGltfInfo.MaterialIndex = meshInfo.MaterialIndex;
                meshGltfInfos.Add(meshGltfInfo);
            }

            // Images
            foreach (var imageInfo in imageInfos)
            {
                gltf.Images.Add(new GltfImage
                {
                    Uri = imageInfo.Uri,
                    MimeType = imageInfo.MimeType
                });
            }

            // Textures
            for (int i = 0; i < imageInfos.Count; i++)
            {
                gltf.Textures.Add(new GltfTexture
                {
                    Source = i
                });
            }

            // Materials
            var textureMap = new Dictionary<string, int>();
            for (int i = 0; i < scene.Materials.Count; i++)
            {
                var material = scene.Materials[i];
                var gltfMaterial = new GltfMaterial
                {
                    Name = $"material_{i}",
                    PbrMetallicRoughness = new GltfPbrMetallicRoughness
                    {
                        BaseColorFactor = new[]
                        {
                            material.DiffuseColor.X,
                            material.DiffuseColor.Y,
                            material.DiffuseColor.Z,
                            1.0f
                        },
                        MetallicFactor = 0.0f,
                        RoughnessFactor = 1.0f
                    }
                };

                // Add texture if available
                if (material.DiffuseTexturePath != null && !string.IsNullOrEmpty(material.DiffuseTexturePath))
                {
                    if (!textureMap.ContainsKey(material.DiffuseTexturePath))
                    {
                        textureMap[material.DiffuseTexturePath] = textureMap.Count;
                    }
                    var textureIndex = textureMap[material.DiffuseTexturePath];

                    if (textureIndex < gltf.Textures.Count)
                    {
                        gltfMaterial.PbrMetallicRoughness.BaseColorTexture = new GltfTextureInfo
                        {
                            Index = textureIndex
                        };
                    }
                }

                gltf.Materials.Add(gltfMaterial);
            }

            // Meshes
            for (int i = 0; i < meshGltfInfos.Count; i++)
            {
                var meshGltfInfo = meshGltfInfos[i];
                var primitive = new GltfPrimitive
                {
                    Attributes = new Dictionary<string, int>
                    {
                        ["POSITION"] = meshGltfInfo.PositionAccessor
                    },
                    Indices = meshGltfInfo.IndexAccessor,
                    Mode = 4 // TRIANGLES
                };

                if (meshGltfInfo.NormalAccessor >= 0)
                {
                    primitive.Attributes["NORMAL"] = meshGltfInfo.NormalAccessor;
                }

                if (meshGltfInfo.TexCoordAccessor >= 0)
                {
                    primitive.Attributes["TEXCOORD_0"] = meshGltfInfo.TexCoordAccessor;
                }

                if (meshGltfInfo.MaterialIndex >= 0 && meshGltfInfo.MaterialIndex < gltf.Materials.Count)
                {
                    primitive.Material = meshGltfInfo.MaterialIndex;
                }

                gltf.Meshes.Add(new GltfMesh
                {
                    Name = $"mesh_{i}",
                    Primitives = new List<GltfPrimitive> { primitive }
                });
            }

            // Nodes
            for (int i = 0; i < gltf.Meshes.Count; i++)
            {
                gltf.Nodes.Add(new GltfNode
                {
                    Mesh = i,
                    Name = $"node_{i}"
                });
            }

            // Scene
            gltf.Scenes.Add(new GltfScene
            {
                Name = "Scene",
                Nodes = Enumerable.Range(0, gltf.Nodes.Count).ToList()
            });
            gltf.Scene = 0;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(gltf, options);
        }

        private class MeshInfo
        {
            public int PositionOffset { get; set; }
            public int PositionCount { get; set; }
            public int NormalOffset { get; set; }
            public int NormalCount { get; set; }
            public int TexCoordOffset { get; set; }
            public int TexCoordCount { get; set; }
            public int IndexOffset { get; set; }
            public int IndexCount { get; set; }
            public Vector3 MinPosition { get; set; }
            public Vector3 MaxPosition { get; set; }
            public int MaterialIndex { get; set; }
        }

        private class MeshGltfInfo
        {
            public int PositionAccessor { get; set; }
            public int NormalAccessor { get; set; } = -1;
            public int TexCoordAccessor { get; set; } = -1;
            public int IndexAccessor { get; set; }
            public int MaterialIndex { get; set; } = -1;
        }

        private class ImageInfo
        {
            public string Uri { get; set; } = "";
            public string MimeType { get; set; } = "";
        }
    }

    // glTF 2.0 JSON structure classes
    public class GltfRoot
    {
        [JsonPropertyName("asset")]
        public GltfAsset Asset { get; set; } = new();

        [JsonPropertyName("scene")]
        public int Scene { get; set; }

        [JsonPropertyName("scenes")]
        public List<GltfScene> Scenes { get; set; } = new();

        [JsonPropertyName("nodes")]
        public List<GltfNode> Nodes { get; set; } = new();

        [JsonPropertyName("meshes")]
        public List<GltfMesh> Meshes { get; set; } = new();

        [JsonPropertyName("materials")]
        public List<GltfMaterial> Materials { get; set; } = new();

        [JsonPropertyName("textures")]
        public List<GltfTexture> Textures { get; set; } = new();

        [JsonPropertyName("images")]
        public List<GltfImage> Images { get; set; } = new();

        [JsonPropertyName("accessors")]
        public List<GltfAccessor> Accessors { get; set; } = new();

        [JsonPropertyName("bufferViews")]
        public List<GltfBufferView> BufferViews { get; set; } = new();

        [JsonPropertyName("buffers")]
        public List<GltfBuffer> Buffers { get; set; } = new();
    }

    public class GltfAsset
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0";

        [JsonPropertyName("generator")]
        public string? Generator { get; set; }
    }

    public class GltfScene
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nodes")]
        public List<int> Nodes { get; set; } = new();
    }

    public class GltfNode
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("mesh")]
        public int? Mesh { get; set; }

        [JsonPropertyName("children")]
        public List<int>? Children { get; set; }
    }

    public class GltfMesh
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("primitives")]
        public List<GltfPrimitive> Primitives { get; set; } = new();
    }

    public class GltfPrimitive
    {
        [JsonPropertyName("attributes")]
        public Dictionary<string, int> Attributes { get; set; } = new();

        [JsonPropertyName("indices")]
        public int? Indices { get; set; }

        [JsonPropertyName("material")]
        public int? Material { get; set; }

        [JsonPropertyName("mode")]
        public int Mode { get; set; } = 4; // TRIANGLES
    }

    public class GltfMaterial
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("pbrMetallicRoughness")]
        public GltfPbrMetallicRoughness? PbrMetallicRoughness { get; set; }
    }

    public class GltfPbrMetallicRoughness
    {
        [JsonPropertyName("baseColorFactor")]
        public float[]? BaseColorFactor { get; set; }

        [JsonPropertyName("baseColorTexture")]
        public GltfTextureInfo? BaseColorTexture { get; set; }

        [JsonPropertyName("metallicFactor")]
        public float MetallicFactor { get; set; } = 1.0f;

        [JsonPropertyName("roughnessFactor")]
        public float RoughnessFactor { get; set; } = 1.0f;
    }

    public class GltfTextureInfo
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("texCoord")]
        public int TexCoord { get; set; } = 0;
    }

    public class GltfTexture
    {
        [JsonPropertyName("source")]
        public int? Source { get; set; }
    }

    public class GltfImage
    {
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }
    }

    public class GltfAccessor
    {
        [JsonPropertyName("bufferView")]
        public int? BufferView { get; set; }

        [JsonPropertyName("byteOffset")]
        public int ByteOffset { get; set; } = 0;

        [JsonPropertyName("componentType")]
        public int ComponentType { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("min")]
        public float[]? Min { get; set; }

        [JsonPropertyName("max")]
        public float[]? Max { get; set; }
    }

    public class GltfBufferView
    {
        [JsonPropertyName("buffer")]
        public int Buffer { get; set; }

        [JsonPropertyName("byteOffset")]
        public int ByteOffset { get; set; }

        [JsonPropertyName("byteLength")]
        public int ByteLength { get; set; }

        [JsonPropertyName("target")]
        public int? Target { get; set; }
    }

    public class GltfBuffer
    {
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("byteLength")]
        public int ByteLength { get; set; }
    }
}
