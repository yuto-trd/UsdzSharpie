using System;
using System.IO;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace UsdzSharpie.Server
{
    public class Texture : IDisposable
    {
        public int Handle { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public Texture(byte[] imageData)
        {
            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            // Load image using StbImageSharp
            StbImage.stbi_set_flip_vertically_on_load(1);

            ImageResult image;
            using (var stream = new MemoryStream(imageData))
            {
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            }

            Width = image.Width;
            Height = image.Height;

            Console.WriteLine($"Loaded texture: {Width}x{Height}, Handle: {Handle}, Components: {image.Comp}");

            // Upload texture data
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

            // Check for OpenGL errors
            ErrorCode error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"OpenGL error after texture upload: {error}");
            }

            // Set texture parameters
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Generate mipmaps
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Use(TextureUnit unit = TextureUnit.Texture0)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            // Verify binding
            GL.GetInteger(GetPName.TextureBinding2D, out int boundTexture);
            if (boundTexture != Handle)
            {
                Console.WriteLine($"Warning: Texture binding failed. Expected {Handle}, got {boundTexture}");
            }
        }

        public void Dispose()
        {
            if (Handle != 0)
            {
                GL.DeleteTexture(Handle);
                Handle = 0;
            }
        }
    }
}
