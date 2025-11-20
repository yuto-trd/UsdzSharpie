using System;

namespace UsdzSharpie.Server
{
    public class RendererService
    {
        public byte[] Render(string usdzPath, CameraViewpoint viewpoint)
        {
            // Create a new OpenGL context for each request
            using var context = new OpenGLContext();
            context.MakeCurrent();

            using var renderer = new OffscreenRenderer(viewpoint.Width, viewpoint.Height);
            return renderer.RenderToImage(usdzPath, viewpoint);
        }
    }
}
