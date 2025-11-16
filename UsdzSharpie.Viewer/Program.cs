using System;
using System.IO;
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
        private MeshRenderer[] meshRenderers;
        private Camera camera;
        private Shader shader;

        private float cameraDistance = 5.0f;
        private float cameraRotationX = 0.0f;
        private float cameraRotationY = 0.0f;
        private Vector2 lastMousePos;
        private bool isDragging = false;

        public UsdzViewer(string usdzPath, GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
            : base(gameWindowSettings, nativeWindowSettings)
        {
            Title = $"USDZ Viewer - {Path.GetFileName(usdzPath)}";
            LoadUsdzFile(usdzPath);
        }

        private void LoadUsdzFile(string path)
        {
            Console.WriteLine($"Loading USDZ file: {path}");

            var reader = new UsdzReader();
            reader.Read(path);
            scene = reader.GetScene();

            if (scene == null)
            {
                Console.WriteLine("Failed to load scene from USDZ file");
                return;
            }

            Console.WriteLine($"Loaded scene with {scene.AllNodes.Count} nodes");
            Console.WriteLine($"Meshes: {scene.Meshes.Count}");
            Console.WriteLine($"Materials: {scene.Materials.Count}");
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
            camera = new Camera(Vector3.UnitZ * cameraDistance, Size.X / (float)Size.Y);

            // Create mesh renderers
            if (scene != null && scene.Meshes.Count > 0)
            {
                var meshList = new System.Collections.Generic.List<MeshRenderer>();
                foreach (var meshNode in scene.GetMeshNodes())
                {
                    if (meshNode.Mesh != null && meshNode.Mesh.Vertices.Length > 0)
                    {
                        var renderer = new MeshRenderer(meshNode.Mesh);
                        renderer.Transform = meshNode.GetWorldTransform();
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

            // Update camera position based on rotation
            float x = (float)(Math.Sin(cameraRotationY) * Math.Cos(cameraRotationX)) * cameraDistance;
            float y = (float)(Math.Sin(cameraRotationX)) * cameraDistance;
            float z = (float)(Math.Cos(cameraRotationY) * Math.Cos(cameraRotationX)) * cameraDistance;
            camera.Position = new Vector3(x, y, z);
            camera.Front = -camera.Position.Normalized();

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

            // Zoom with mouse wheel
            var mouse = MouseState;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            cameraDistance -= e.OffsetY * 0.5f;
            cameraDistance = Math.Clamp(cameraDistance, 0.5f, 50.0f);
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
            }
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDragging)
            {
                var currentMousePos = new Vector2(e.X, e.Y);
                var delta = currentMousePos - lastMousePos;

                cameraRotationY += delta.X * 0.005f;
                cameraRotationX += delta.Y * 0.005f;

                // Clamp vertical rotation
                cameraRotationX = Math.Clamp(cameraRotationX, -MathF.PI / 2 + 0.1f, MathF.PI / 2 - 0.1f);

                lastMousePos = currentMousePos;
            }
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
                Console.WriteLine("Usage: UsdzSharpie.Viewer <path_to_usdz_file>");
                Console.WriteLine("\nExample USDZ files available in Examples folder:");
                var examplesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Examples");
                if (Directory.Exists(examplesPath))
                {
                    var usdzFiles = Directory.GetFiles(examplesPath, "*.usdz");
                    foreach (var file in usdzFiles)
                    {
                        Console.WriteLine($"  {Path.GetFileName(file)}");
                    }
                }
                return;
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
