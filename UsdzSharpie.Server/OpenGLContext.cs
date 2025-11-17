using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

namespace UsdzSharpie.Server
{
    public class OpenGLContext : IDisposable
    {
        private IntPtr context;
        private IntPtr buffer;
        private int width;
        private int height;
        private static OpenGLContext? instance;
        private static readonly object lockObject = new object();

        public static OpenGLContext Instance
        {
            get
            {
                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = new OpenGLContext();
                        instance.Initialize();
                    }
                    return instance;
                }
            }
        }

        private void Initialize()
        {
            // Create a small initial context just for initialization
            width = 1;
            height = 1;

            // Create OSMesa context with RGBA format and depth buffer
            context = OSMesa.OSMesaCreateContextExt(
                OSMesa.OSMESA_RGBA,
                24,  // depth bits
                8,   // stencil bits
                0,   // accum bits
                IntPtr.Zero);

            if (context == IntPtr.Zero)
            {
                throw new Exception("Failed to create OSMesa context");
            }

            // Allocate buffer for rendering
            buffer = Marshal.AllocHGlobal(width * height * 4);

            // Make context current
            if (!OSMesa.OSMesaMakeCurrent(context, buffer, OSMesa.GL_UNSIGNED_BYTE, width, height))
            {
                throw new Exception("Failed to make OSMesa context current");
            }

            // Load OpenGL function pointers using OSMesa
            // OpenTK needs a custom bindings context for OSMesa
            var loader = new OSMesaProcLoader();
            typeof(GL).GetMethod("LoadBindings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?
                .Invoke(null, new object[] { loader });

            Console.WriteLine($"OSMesa Context initialized:");
            Console.WriteLine($"  Vendor: {GL.GetString(StringName.Vendor)}");
            Console.WriteLine($"  Renderer: {GL.GetString(StringName.Renderer)}");
            Console.WriteLine($"  Version: {GL.GetString(StringName.Version)}");
            Console.WriteLine($"  GLSL Version: {GL.GetString(StringName.ShadingLanguageVersion)}");
        }

        public void MakeCurrent()
        {
            if (context != IntPtr.Zero && buffer != IntPtr.Zero)
            {
                OSMesa.OSMesaMakeCurrent(context, buffer, OSMesa.GL_UNSIGNED_BYTE, width, height);
            }
        }

        public void Dispose()
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            if (context != IntPtr.Zero)
            {
                OSMesa.OSMesaDestroyContext(context);
                context = IntPtr.Zero;
            }
        }

        private class OSMesaProcLoader
        {
            public IntPtr GetProcAddress(string procName)
            {
                return OSMesa.OSMesaGetProcAddress(procName);
            }
        }
    }
}
