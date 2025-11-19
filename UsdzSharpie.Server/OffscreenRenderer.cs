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

        public byte[] RenderToImage(string usdzPath, CameraViewpoint viewpoint)
        {
            if (!isInitialized)
                Initialize();

            // Try loading USDZ file using UsdzReader first
            var meshRenderers = new List<MeshRenderer>();
            var textures = new HashSet<Texture>();
            var boundingBox = new BoundingBox();
            bool usedAssimp = false;
            bool enableLighting = viewpoint.EnableLighting;

            try
            {
                Console.WriteLine($"Attempting to load USDZ with UsdzReader: {usdzPath}");
                var reader = new UsdzReader();
                reader.Read(usdzPath);
                var usdcScene = reader.GetScene();

                if (usdcScene != null && usdcScene.Meshes.Count > 0)
                {
                    Console.WriteLine($"Successfully loaded USDZ with UsdzReader. Meshes: {usdcScene.Meshes.Count}");

                    UsdcMaterial? defaultMaterial = usdcScene.Materials.Values.FirstOrDefault();

                    // Use UsdcMesh directly with MeshRenderer
                    foreach (var meshNode in usdcScene.GetMeshNodes())
                    {
                        var usdcMesh = meshNode.Mesh;

                        if (usdcMesh.Vertices != null && usdcMesh.Vertices.Length > 0)
                        {
                            var renderer = new MeshRenderer(usdcMesh);
                            var transform = meshNode.GetWorldTransform();
                            renderer.Transform = new Matrix4(
                                (float)transform.M00, (float)transform.M01, (float)transform.M02, (float)transform.M03,
                                (float)transform.M10, (float)transform.M11, (float)transform.M12, (float)transform.M13,
                                (float)transform.M20, (float)transform.M21, (float)transform.M22, (float)transform.M23,
                                (float)transform.M30, (float)transform.M31, (float)transform.M32, (float)transform.M33
                            );

                            // Try to get material color
                            UsdcMaterial? material = null;
                            if (!string.IsNullOrEmpty(meshNode.Mesh.MaterialPath) && usdcScene.Materials.ContainsKey(meshNode.Mesh.MaterialPath))
                            {
                                material = usdcScene.Materials[meshNode.Mesh.MaterialPath];
                            }
                            else if (defaultMaterial != null)
                            {
                                material = defaultMaterial;
                            }

                            if (material != null)
                            {
                                renderer.Color = new Vector3(material.DiffuseColor.X, material.DiffuseColor.Y, material.DiffuseColor.Z);
                                Console.WriteLine($"  Material color: ({material.DiffuseColor.X}, {material.DiffuseColor.Y}, {material.DiffuseColor.Z})");

                                // Try to load diffuse texture
                                if (!string.IsNullOrEmpty(material.DiffuseTexture))
                                {
                                    var textureData = reader.GetTexture(material.DiffuseTexture);
                                    if (textureData != null)
                                    {
                                        try
                                        {
                                            renderer.DiffuseTexture = new Texture(textureData);
                                            Console.WriteLine($"  Loaded texture: {material.DiffuseTexture}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"  Failed to load texture: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"  Texture not found: {material.DiffuseTexture}");
                                    }
                                }
                            }
                            else
                            {
                                // Default red color if no material
                                renderer.Color = new Vector3(0.8f, 0.2f, 0.2f);
                            }

                            meshRenderers.Add(renderer);

                            // Update bounding box
                            foreach (var vertex in usdcMesh.Vertices)
                            {
                                var worldPos = new Vector4(vertex.X, vertex.Y, vertex.Z, 1.0f);
                                worldPos = renderer.Transform * worldPos;
                                boundingBox.Expand(new Vector3(worldPos.X, worldPos.Y, worldPos.Z));
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("UsdzReader returned null or empty scene, trying Assimp...");
                    usedAssimp = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UsdzReader failed with error: {ex.Message}");
                Console.WriteLine("Falling back to Assimp loader...");
                usedAssimp = true;
            }

            // Fallback to Assimp if UsdzReader failed
            if (usedAssimp)
            {
                using var assimpLoader = new AssimpLoader();
                var scene = assimpLoader.LoadUsdz(usdzPath);

                if (scene == null || scene.Meshes.Count == 0)
                {
                    throw new Exception("Failed to load scene from USDZ file or scene is empty (both UsdzReader and Assimp failed)");
                }

                Console.WriteLine($"Assimp loaded scene successfully. Meshes: {scene.Meshes.Count}");

                // Build scene from Assimp data
                foreach (var (mesh, transform) in scene.GetAllMeshes())
                {
                    if (mesh.Vertices.Length > 0)
                    {
                        var renderer = new MeshRenderer(mesh, transform);

                        // Load texture if available
                        if (mesh.Material?.DiffuseTextureData != null)
                        {
                            var texture = new Texture(mesh.Material.DiffuseTextureData);
                            renderer.DiffuseTexture = texture;
                            textures.Add(texture);
                        }

                        meshRenderers.Add(renderer);

                        // Update bounding box
                        foreach (var vertex in mesh.Vertices)
                        {
                            var worldPos = new Vector4(vertex.X, vertex.Y, vertex.Z, 1.0f);
                            worldPos = transform * worldPos;
                            boundingBox.Expand(new Vector3(worldPos.X, worldPos.Y, worldPos.Z));
                        }
                    }
                }
            }

            if (meshRenderers.Count == 0)
            {
                throw new Exception("No valid meshes found in USDZ file");
            }

            // Calculate bounding box properties
            var boundingBoxSize = boundingBox.GetSize();
            var boundingBoxCenter = boundingBox.GetCenter();

            // Calculate camera position relative to object size
            // viewpoint.Position is interpreted as a multiplier of the bounding box size
            var actualCameraPosition = boundingBoxCenter + new Vector3(
                boundingBoxSize.X * viewpoint.Position.X,
                boundingBoxSize.Y * viewpoint.Position.Y,
                boundingBoxSize.Z * viewpoint.Position.Z
            );

            // If target is zero, use bounding box center as target
            var actualTarget = viewpoint.Target == Vector3.Zero ? boundingBoxCenter : viewpoint.Target;

            Console.WriteLine($"Bounding Box - Min: {boundingBox.Min}, Max: {boundingBox.Max}");
            Console.WriteLine($"Bounding Box - Size: {boundingBoxSize}, Center: {boundingBoxCenter}");
            Console.WriteLine($"Camera - Position Multiplier: {viewpoint.Position}, Actual Position: {actualCameraPosition}, Target: {actualTarget}");

            // Setup camera
            float aspectRatio = (float)width / height;
            var camera = new Camera(actualCameraPosition, actualTarget, aspectRatio, viewpoint.Fov);

            // Render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.Viewport(0, 0, width, height);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);

            var shader = new Shader();
            shader.Use();

            var view = camera.GetViewMatrix();
            var projection = camera.GetProjectionMatrix();
            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetVector3("viewPos", camera.Position);
            shader.SetVector3("lightPos", new Vector3(5, 5, 5));
            shader.SetBool("enableLighting", enableLighting);

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
            foreach (var texture in textures)
            {
                texture.Dispose();
            }

            // Encode image using SkiaSharp
            return EncodeImage(pixels, width, height);
        }

        private byte[] EncodeImage(byte[] pixels, int width, int height)
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
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);

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
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool EnableLighting { get; set; } = true;
    }

    public class BoundingBox
    {
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }

        public BoundingBox()
        {
            Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        }

        public void Expand(Vector3 point)
        {
            Min = new Vector3(
                Math.Min(Min.X, point.X),
                Math.Min(Min.Y, point.Y),
                Math.Min(Min.Z, point.Z)
            );
            Max = new Vector3(
                Math.Max(Max.X, point.X),
                Math.Max(Max.Y, point.Y),
                Math.Max(Max.Z, point.Z)
            );
        }

        public Vector3 GetSize()
        {
            return Max - Min;
        }

        public Vector3 GetCenter()
        {
            return (Min + Max) * 0.5f;
        }
    }
}
