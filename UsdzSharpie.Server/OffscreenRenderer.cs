using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using SkiaSharp;
using UsdzSharpie;

namespace UsdzSharpie.Server
{
    public class OffscreenRenderer : IDisposable
    {
        private readonly int width;
        private readonly int height;
        private int framebuffer;
        private int colorRenderbuffer;
        private int depthRenderbuffer;
        private bool isInitialized = false;

        public OffscreenRenderer(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public void Initialize()
        {
            if (isInitialized)
                return;

            // Create framebuffer
            framebuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);

            // Create color renderbuffer
            colorRenderbuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, colorRenderbuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                RenderbufferTarget.Renderbuffer, colorRenderbuffer);

            // Create depth renderbuffer
            depthRenderbuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, depthRenderbuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, depthRenderbuffer);

            // Check framebuffer completeness
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception($"Framebuffer is not complete: {status}");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            isInitialized = true;

            Console.WriteLine($"Offscreen renderer initialized: {width}x{height}");
        }

        public byte[] RenderToImage(string usdzPath, CameraViewpoint[] viewpoints, ImageFormat format = ImageFormat.Png)
        {
            if (!isInitialized)
                Initialize();

            // For simplicity, render only the first viewpoint
            // You can extend this to handle multiple viewpoints and return multiple images
            var viewpoint = viewpoints.Length > 0 ? viewpoints[0] : new CameraViewpoint
            {
                Position = new Vector3(0.1f, 0.1f, 0.1f),
                Target = Vector3.Zero,
                Fov = 45.0f
            };

            // Load USDZ file
            var usdzReader = new UsdzReader();
            usdzReader.Read(usdzPath);
            var scene = usdzReader.GetScene();

            if (scene == null)
            {
                throw new Exception("Failed to load scene from USDZ file");
            }

            // Build scene
            var meshRenderers = new List<MeshRenderer>();
            var textures = new Dictionary<string, Texture>();

            foreach (var meshNode in scene.GetMeshNodes())
            {
                if (meshNode.Mesh != null && meshNode.Mesh.Vertices.Length > 0)
                {
                    var renderer = new MeshRenderer(meshNode.Mesh);
                    renderer.Transform = meshNode.GetWorldTransform();

                    // Load material
                    UsdcMaterial? material = null;
                    if (!string.IsNullOrEmpty(meshNode.Mesh.MaterialPath) && scene.Materials.ContainsKey(meshNode.Mesh.MaterialPath))
                    {
                        material = scene.Materials[meshNode.Mesh.MaterialPath];
                    }

                    if (material != null)
                    {
                        renderer.Color = new Vector3(material.DiffuseColor.X, material.DiffuseColor.Y, material.DiffuseColor.Z);

                        // Load texture if available
                        if (!string.IsNullOrEmpty(material.DiffuseTexture))
                        {
                            if (!textures.TryGetValue(material.DiffuseTexture, out var texture))
                            {
                                var imageData = usdzReader.GetTexture(material.DiffuseTexture);
                                if (imageData != null)
                                {
                                    texture = new Texture(imageData);
                                    textures[material.DiffuseTexture] = texture;
                                }
                            }
                            renderer.DiffuseTexture = texture;
                        }
                    }

                    meshRenderers.Add(renderer);
                }
            }

            // Setup camera
            float aspectRatio = (float)width / height;
            var camera = new Camera(viewpoint.Position, viewpoint.Target, aspectRatio, viewpoint.Fov);

            // Render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.Viewport(0, 0, width, height);
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.CullFace(TriangleFace.Back);

            var shader = new Shader();
            shader.Use();

            var view = camera.GetViewMatrix();
            var projection = camera.GetProjectionMatrix();
            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetVector3("viewPos", camera.Position);
            shader.SetVector3("lightPos", new Vector3(5, 5, 5));

            foreach (var renderer in meshRenderers)
            {
                renderer.Draw(shader);
            }

            // Read pixels
            byte[] pixels = new byte[width * height * 4];
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.ReadPixels(0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            // Cleanup
            shader.Dispose();
            foreach (var renderer in meshRenderers)
            {
                renderer.Dispose();
            }
            foreach (var texture in textures.Values)
            {
                texture.Dispose();
            }

            // Encode image using SkiaSharp
            return EncodeImage(pixels, width, height, format);
        }

        private byte[] EncodeImage(byte[] pixels, int width, int height, ImageFormat format)
        {
            // Create a bitmap from the pixel data
            var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            // Flip the image vertically (OpenGL renders bottom-up, we want top-down)
            var flippedPixels = new byte[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * width * 4;
                int dstOffset = (height - 1 - y) * width * 4;
                Array.Copy(pixels, srcOffset, flippedPixels, dstOffset, width * 4);
            }

            unsafe
            {
                fixed (byte* ptr = flippedPixels)
                {
                    using var bitmap = new SKBitmap();
                    bitmap.InstallPixels(imageInfo, (IntPtr)ptr);

                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = format switch
                    {
                        ImageFormat.Png => image.Encode(SKEncodedImageFormat.Png, 100),
                        ImageFormat.Jpeg => image.Encode(SKEncodedImageFormat.Jpeg, 90),
                        ImageFormat.Webp => image.Encode(SKEncodedImageFormat.Webp, 90),
                        _ => image.Encode(SKEncodedImageFormat.Png, 100)
                    };

                    return data.ToArray();
                }
            }
        }

        public void Dispose()
        {
            if (framebuffer != 0)
            {
                GL.DeleteFramebuffer(framebuffer);
                framebuffer = 0;
            }

            if (colorRenderbuffer != 0)
            {
                GL.DeleteRenderbuffer(colorRenderbuffer);
                colorRenderbuffer = 0;
            }

            if (depthRenderbuffer != 0)
            {
                GL.DeleteRenderbuffer(depthRenderbuffer);
                depthRenderbuffer = 0;
            }

            isInitialized = false;
        }
    }

    public class CameraViewpoint
    {
        public Vector3 Position { get; set; }
        public Vector3 Target { get; set; }
        public float Fov { get; set; } = 45.0f;
    }

    public enum ImageFormat
    {
        Png,
        Jpeg,
        Webp
    }
}
