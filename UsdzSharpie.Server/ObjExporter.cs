using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OpenTK.Mathematics;

namespace UsdzSharpie.Server
{
    public class ObjExporter
    {
        public static byte[] ExportToZip(AssimpScene scene, string modelName = "model")
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                // Generate OBJ content
                var objContent = GenerateObjContent(scene, modelName);
                var objEntry = archive.CreateEntry($"{modelName}.obj");
                using (var writer = new StreamWriter(objEntry.Open()))
                {
                    writer.Write(objContent);
                }

                // Generate MTL content
                var mtlContent = GenerateMtlContent(scene, modelName);
                var mtlEntry = archive.CreateEntry($"{modelName}.mtl");
                using (var writer = new StreamWriter(mtlEntry.Open()))
                {
                    writer.Write(mtlContent);
                }

                // Add textures
                var textureMap = new Dictionary<string, int>();
                int textureIndex = 0;
                foreach (var material in scene.Materials)
                {
                    if (material.DiffuseTextureData != null && material.DiffuseTexturePath != null)
                    {
                        var textureName = GetSafeTextureName(material.DiffuseTexturePath, textureIndex);
                        if (!textureMap.ContainsKey(material.DiffuseTexturePath))
                        {
                            textureMap[material.DiffuseTexturePath] = textureIndex;
                            var textureEntry = archive.CreateEntry(textureName);
                            using (var stream = textureEntry.Open())
                            {
                                stream.Write(material.DiffuseTextureData, 0, material.DiffuseTextureData.Length);
                            }
                            textureIndex++;
                        }
                    }
                }
            }

            return memoryStream.ToArray();
        }

        private static string GenerateObjContent(AssimpScene scene, string modelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# OBJ file generated from USDZ");
            sb.AppendLine($"# Model: {modelName}");
            sb.AppendLine($"mtllib {modelName}.mtl");
            sb.AppendLine();

            int vertexOffset = 1;
            int normalOffset = 1;
            int texCoordOffset = 1;

            var meshes = scene.GetAllMeshes().ToList();

            // Write all vertices, normals, and texture coordinates
            foreach (var (mesh, transform) in meshes)
            {
                sb.AppendLine($"# Mesh: {mesh.Name}");

                // Transform and write vertices
                foreach (var vertex in mesh.Vertices)
                {
                    var transformed = Vector3.TransformPosition(vertex, transform);
                    sb.AppendLine($"v {FormatFloat(transformed.X)} {FormatFloat(transformed.Y)} {FormatFloat(transformed.Z)}");
                }

                // Transform and write normals
                if (mesh.Normals.Length > 0)
                {
                    var normalMatrix = transform.ClearTranslation();
                    foreach (var normal in mesh.Normals)
                    {
                        var transformed = Vector3.TransformNormal(normal, normalMatrix);
                        transformed = Vector3.Normalize(transformed);
                        sb.AppendLine($"vn {FormatFloat(transformed.X)} {FormatFloat(transformed.Y)} {FormatFloat(transformed.Z)}");
                    }
                }

                // Write texture coordinates
                // Note: Assimp loads with FlipUVs flag, so we need to flip back for OBJ format
                if (mesh.TexCoords.Length > 0)
                {
                    foreach (var texCoord in mesh.TexCoords)
                    {
                        // Flip V coordinate back (Assimp already flipped it with FlipUVs flag)
                        sb.AppendLine($"vt {FormatFloat(texCoord.X)} {FormatFloat(1.0f - texCoord.Y)}");
                    }
                }
            }

            sb.AppendLine();

            // Write faces
            int meshIndex = 0;
            foreach (var (mesh, transform) in meshes)
            {
                sb.AppendLine($"# Mesh: {mesh.Name}");

                // Set material
                var materialIndex = scene.Meshes.IndexOf(mesh);
                if (materialIndex >= 0 && materialIndex < scene.Materials.Count)
                {
                    sb.AppendLine($"usemtl material_{materialIndex}");
                }
                else
                {
                    sb.AppendLine($"usemtl default_material");
                }

                // Write faces
                bool hasNormals = mesh.Normals.Length > 0;
                bool hasTexCoords = mesh.TexCoords.Length > 0;

                for (int i = 0; i < mesh.Indices.Length; i += 3)
                {
                    sb.Append("f");
                    for (int j = 0; j < 3; j++)
                    {
                        var index = mesh.Indices[i + j];
                        var v = vertexOffset + index;

                        sb.Append($" {v}");

                        if (hasTexCoords)
                        {
                            var vt = texCoordOffset + index;
                            sb.Append($"/{vt}");
                        }
                        else if (hasNormals)
                        {
                            sb.Append("/");
                        }

                        if (hasNormals)
                        {
                            var vn = normalOffset + index;
                            sb.Append($"/{vn}");
                        }
                    }
                    sb.AppendLine();
                }

                vertexOffset += mesh.Vertices.Length;
                normalOffset += mesh.Normals.Length;
                texCoordOffset += mesh.TexCoords.Length;
                meshIndex++;
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string GenerateMtlContent(AssimpScene scene, string modelName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# MTL file generated from USDZ");
            sb.AppendLine($"# Model: {modelName}");
            sb.AppendLine();

            var textureMap = new Dictionary<string, int>();
            int textureIndex = 0;

            for (int i = 0; i < scene.Materials.Count; i++)
            {
                var material = scene.Materials[i];
                sb.AppendLine($"newmtl material_{i}");

                // Ambient color (usually same as diffuse)
                sb.AppendLine($"Ka {FormatFloat(material.DiffuseColor.X)} {FormatFloat(material.DiffuseColor.Y)} {FormatFloat(material.DiffuseColor.Z)}");

                // Diffuse color
                sb.AppendLine($"Kd {FormatFloat(material.DiffuseColor.X)} {FormatFloat(material.DiffuseColor.Y)} {FormatFloat(material.DiffuseColor.Z)}");

                // Specular color (default white with low intensity)
                sb.AppendLine("Ks 0.5 0.5 0.5");

                // Specular exponent
                sb.AppendLine("Ns 32.0");

                // Optical density (index of refraction)
                sb.AppendLine("Ni 1.0");

                // Dissolve (transparency): 1.0 = fully opaque
                sb.AppendLine("d 1.0");

                // Illumination model: 2 = highlight on
                sb.AppendLine("illum 2");

                // Diffuse texture
                if (material.DiffuseTextureData != null && material.DiffuseTexturePath != null)
                {
                    string textureName;
                    if (!textureMap.ContainsKey(material.DiffuseTexturePath))
                    {
                        textureMap[material.DiffuseTexturePath] = textureIndex;
                        textureName = GetSafeTextureName(material.DiffuseTexturePath, textureIndex);
                        textureIndex++;
                    }
                    else
                    {
                        textureName = GetSafeTextureName(material.DiffuseTexturePath, textureMap[material.DiffuseTexturePath]);
                    }

                    sb.AppendLine($"map_Kd {textureName}");
                }

                sb.AppendLine();
            }

            // Add default material for meshes without materials
            sb.AppendLine("newmtl default_material");
            sb.AppendLine("Ka 0.8 0.8 0.8");
            sb.AppendLine("Kd 0.8 0.8 0.8");
            sb.AppendLine("Ks 0.5 0.5 0.5");
            sb.AppendLine("Ns 32.0");
            sb.AppendLine("Ni 1.0");
            sb.AppendLine("d 1.0");
            sb.AppendLine("illum 2");

            return sb.ToString();
        }

        private static string GetSafeTextureName(string originalPath, int index)
        {
            var extension = Path.GetExtension(originalPath);
            var fileName = Path.GetFileNameWithoutExtension(originalPath);

            // Sanitize filename
            var invalidChars = Path.GetInvalidFileNameChars();
            fileName = string.Join("_", fileName.Split(invalidChars));

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"texture_{index}";
            }

            return $"{fileName}{extension}";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }
}
