using System;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using UsdzSharpie;

namespace UsdzSharpie.Viewer
{
    public class UsdzViewer : GameWindow
    {
        private UsdcScene scene;
        private UsdzReader usdzReader;
        private MeshRenderer[] meshRenderers;
        private Camera camera;
        private Shader shader;

        private float cameraSpeed = 2.5f;
        private float mouseSensitivity = 0.1f;
        private Vector2 lastMousePos;
        private bool isDragging = false;
        private bool firstMouse = true;

        public UsdzViewer(string usdzPath, GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            Title = $"USDZ Viewer - {Path.GetFileName(usdzPath)}";
            LoadUsdzFile(usdzPath);
        }

        private void LoadUsdzFile(string path)
        {
            Console.WriteLine($"Loading USDZ file: {path}");

            usdzReader = new UsdzReader();
            usdzReader.Read(path);
            scene = usdzReader.GetScene();

            if (scene == null)
            {
                Console.WriteLine("Failed to load scene from USDZ file");
                return;
            }

            Console.WriteLine($"Loaded scene with {scene.AllNodes.Count} nodes");
            Console.WriteLine($"Meshes: {scene.Meshes.Count}");
            Console.WriteLine($"Materials: {scene.Materials.Count}");
            Console.WriteLine($"Textures: {usdzReader.GetAllTextures().Count}");
        }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            // Create shader
            shader = new Shader();

            // Initialize camera
            camera = new Camera(new Vector3(0.0f, 1.0f, 5.0f), Size.X / (float)Size.Y);

            // Create mesh renderers
            if (scene != null && scene.Meshes.Count > 0)
            {
                // Get the first material (if any)
                UsdcMaterial defaultMaterial = null;
                if (scene.Materials.Count > 0)
                {
                    defaultMaterial = scene.Materials.Values.First();
                    Console.WriteLine($"Using default material with color: ({defaultMaterial.DiffuseColor.X}, {defaultMaterial.DiffuseColor.Y}, {defaultMaterial.DiffuseColor.Z})");
                }

                var meshList = new System.Collections.Generic.List<MeshRenderer>();
                foreach (var meshNode in scene.GetMeshNodes())
                {
                    if (meshNode.Mesh != null && meshNode.Mesh.Vertices.Length > 0)
                    {
                        var renderer = new MeshRenderer(meshNode.Mesh);
                        renderer.Transform = meshNode.GetWorldTransform();

                        // Try to get material color
                        UsdcMaterial? material = null;
                        if (!string.IsNullOrEmpty(meshNode.Mesh.MaterialPath) && scene.Materials.ContainsKey(meshNode.Mesh.MaterialPath))
                        {
                            material = scene.Materials[meshNode.Mesh.MaterialPath];
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
                                var textureData = usdzReader.GetTexture(material.DiffuseTexture);
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

                        meshList.Add(renderer);
                        Console.WriteLine($"Created renderer for mesh: {meshNode.Name} with {meshNode.Mesh.Vertices.Length} vertices");
                    }
                }
                meshRenderers = meshList.ToArray();
                Console.WriteLine($"Created {meshRenderers.Length} mesh renderers");
            }
            else
            {
                Console.WriteLine("No meshes to render");
                meshRenderers = new MeshRenderer[0];
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            shader.Use();

            // Set view and projection matrices
            var view = camera.GetViewMatrix();
            var projection = camera.GetProjectionMatrix();

            shader.SetMatrix4("view", view);
            shader.SetMatrix4("projection", projection);
            shader.SetVector3("viewPos", camera.Position);
            shader.SetVector3("lightPos", new Vector3(2.0f, 2.0f, 2.0f));

            // Render all meshes
            if (meshRenderers != null)
            {
                foreach (var renderer in meshRenderers)
                {
                    renderer.Draw(shader);
                }
            }

            SwapBuffers();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            var input = KeyboardState;

            if (input.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            // Calculate movement speed based on frame time
            float velocity = cameraSpeed * (float)args.Time;

            // WASD movement
            if (input.IsKeyDown(Keys.W))
            {
                camera.Position += camera.Front * velocity;
            }
            if (input.IsKeyDown(Keys.S))
            {
                camera.Position -= camera.Front * velocity;
            }
            if (input.IsKeyDown(Keys.A))
            {
                camera.Position -= camera.Right * velocity;
            }
            if (input.IsKeyDown(Keys.D))
            {
                camera.Position += camera.Right * velocity;
            }

            // E/Q for up/down movement
            if (input.IsKeyDown(Keys.E))
            {
                camera.Position += camera.Up * velocity;
            }
            if (input.IsKeyDown(Keys.Q))
            {
                camera.Position -= camera.Up * velocity;
            }

            // Shift for faster movement
            if (input.IsKeyDown(Keys.LeftShift))
            {
                cameraSpeed = 5.0f;
            }
            else
            {
                cameraSpeed = 2.5f;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            // Adjust FOV for zoom effect
            camera.Fov -= e.OffsetY * 2.0f;
            camera.Fov = Math.Clamp(camera.Fov, 1.0f, 90.0f);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButton.Left)
            {
                isDragging = true;
                lastMousePos = new Vector2(MouseState.X, MouseState.Y);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButton.Left)
            {
                isDragging = false;
                firstMouse = true;
            }
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDragging)
            {
                var currentMousePos = new Vector2(e.X, e.Y);

                if (firstMouse)
                {
                    lastMousePos = currentMousePos;
                    firstMouse = false;
                }

                var delta = currentMousePos - lastMousePos;

                camera.Yaw += delta.X * mouseSensitivity;
                camera.Pitch -= delta.Y * mouseSensitivity;

                // Clamp vertical rotation
                camera.Pitch = Math.Clamp(camera.Pitch, -89.0f, 89.0f);

                // Update camera vectors
                UpdateCameraVectors();

                lastMousePos = currentMousePos;
            }
        }

        private void UpdateCameraVectors()
        {
            Vector3 front;
            front.X = (float)(Math.Cos(MathHelper.DegreesToRadians(camera.Yaw)) * Math.Cos(MathHelper.DegreesToRadians(camera.Pitch)));
            front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(camera.Pitch));
            front.Z = (float)(Math.Sin(MathHelper.DegreesToRadians(camera.Yaw)) * Math.Cos(MathHelper.DegreesToRadians(camera.Pitch)));
            camera.Front = Vector3.Normalize(front);

            camera.Right = Vector3.Normalize(Vector3.Cross(camera.Front, camera.WorldUp));
            camera.Up = Vector3.Normalize(Vector3.Cross(camera.Right, camera.Front));
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            GL.Viewport(0, 0, Size.X, Size.Y);
            if (camera != null)
            {
                camera.AspectRatio = Size.X / (float)Size.Y;
            }
        }

        protected override void OnUnload()
        {
            base.OnUnload();

            // Cleanup
            if (meshRenderers != null)
            {
                foreach (var renderer in meshRenderers)
                {
                    renderer.Dispose();
                }
            }

            shader?.Dispose();
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                // Console.WriteLine("Usage: UsdzSharpie.Viewer <path_to_usdz_file>");
                // Console.WriteLine("\nExample USDZ files available in Examples folder:");
                // var examplesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Examples");
                // if (Directory.Exists(examplesPath))
                // {
                //     var usdzFiles = Directory.GetFiles(examplesPath, "*.usdz");
                //     foreach (var file in usdzFiles)
                //     {
                //         Console.WriteLine($"  {Path.GetFileName(file)}");
                //     }
                // }
                // return;
                args = ["/Users/teradatakeshishou/Documents/source/名称未設定オブジェクト.usdz"];
            }

            string usdzPath = args[0];
            if (!File.Exists(usdzPath))
            {
                Console.WriteLine($"File not found: {usdzPath}");
                return;
            }

            var gameWindowSettings = GameWindowSettings.Default;
            var nativeWindowSettings = new NativeWindowSettings()
            {
                ClientSize = new Vector2i(1280, 720),
                Title = "USDZ Viewer",
                Flags = ContextFlags.ForwardCompatible,
            };

            using (var viewer = new UsdzViewer(usdzPath, gameWindowSettings, nativeWindowSettings))
            {
                viewer.Run();
            }
        }
    }
}
