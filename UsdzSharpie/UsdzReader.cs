using System;
using System.IO;
using System.IO.Compression;

namespace UsdzSharpie
{
    public class UsdzReader
    {
        private UsdcReader mainUsdcReader;
        private System.Collections.Generic.Dictionary<string, byte[]> textures = new System.Collections.Generic.Dictionary<string, byte[]>();

        public void Read(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                ReadUsdz(stream);
            }
        }

        public void ReadUsdz(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                ReadUsdz(stream);
            }
        }

        public void ReadUsdz(Stream stream)
        {
            using (var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ValidateUsdz(stream, zipArchive);

                foreach (var entry in zipArchive.Entries)
                {
                    var ext = Path.GetExtension(entry.FullName).ToLower();

                    if (ext.Equals(".usdc"))
                    {
                        var usdcReader = new UsdcReader();
                        {
                            using (var entryStream = entry.Open())
                            using (var memoryStream = new MemoryStream())
                            {
                                entryStream.CopyTo(memoryStream);
                                memoryStream.Position = 0;
                                usdcReader.ReadUsdc(memoryStream);
                            }
                        }

                        // Store the first USDC reader as the main scene
                        if (mainUsdcReader == null)
                        {
                            mainUsdcReader = usdcReader;
                        }
                    }
                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    {
                        // Extract texture data
                        using (var entryStream = entry.Open())
                        using (var memoryStream = new MemoryStream())
                        {
                            entryStream.CopyTo(memoryStream);
                            textures[entry.FullName] = memoryStream.ToArray();
                            Logger.LogLine($"Extracted texture: {entry.FullName} ({memoryStream.Length} bytes)");
                        }
                    }
                }
            }
        }

        public UsdcScene GetScene()
        {
            return mainUsdcReader?.GetScene();
        }

        public byte[] GetTexture(string texturePath)
        {
            // Clean up the path
            var cleanPath = texturePath.TrimStart('/');

            if (textures.ContainsKey(cleanPath))
            {
                return textures[cleanPath];
            }

            // Try with different variations
            foreach (var key in textures.Keys)
            {
                if (key.EndsWith(Path.GetFileName(texturePath)))
                {
                    return textures[key];
                }
            }

            return null;
        }

        public System.Collections.Generic.Dictionary<string, byte[]> GetAllTextures()
        {
            return textures;
        }

        private void ValidateUsdz(Stream stream, ZipArchive zipArchive)
        {
            foreach (var entry in zipArchive.Entries)
            {
                using (var entryStream = entry.Open())
                {
                    var offset = stream.Position;
                    if (offset % 64 != 0)
                    {
                        throw new Exception("Zip entry offset must be mulitple of 64 bytes");
                    }
                    Logger.LogLine($"offset = {offset}");
                }
            }
            for (int i = 0; i < zipArchive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = zipArchive.Entries[i];
                using (var entryStream = entry.Open())
                {
                    Logger.LogLine($"[{i}] {entry.FullName} : byte range ({stream.Position}, {stream.Position + entry.Length})");
                }
            }
        }
    }
}
