using System;

namespace UsdzSharpie.Server
{
    public class RendererService
    {
        private readonly object lockObject = new object();
        private bool isInitialized = false;

        public void Initialize()
        {
            lock (lockObject)
            {
                if (!isInitialized)
                {
                    // Initialize OpenGL context
                    var context = OpenGLContext.Instance;
                    context.MakeCurrent();
                    isInitialized = true;
                    Console.WriteLine("RendererService initialized");
                }
            }
        }

        public byte[] Render(string usdzPath, CameraViewpoint[] viewpoints, int width, int height, ImageFormat format, bool enableLighting = true)
        {
            lock (lockObject)
            {
                if (!isInitialized)
                {
                    throw new InvalidOperationException("RendererService not initialized");
                }

                // Make sure OpenGL context is current for this thread
                OpenGLContext.Instance.MakeCurrent();

                using var renderer = new OffscreenRenderer(width, height);
                return renderer.RenderToImage(usdzPath, viewpoints, format, enableLighting);
            }
        }
    }
}
