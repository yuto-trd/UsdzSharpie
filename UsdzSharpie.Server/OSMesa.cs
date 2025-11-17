using System;
using System.Runtime.InteropServices;

namespace UsdzSharpie.Server
{
    /// <summary>
    /// P/Invoke bindings for OSMesa (Off-Screen Mesa)
    /// </summary>
    public static class OSMesa
    {
        // OSMesa library name varies by platform
        private const string LibraryName = "OSMesa";

        // OSMesa pixel formats
        public const int OSMESA_COLOR_INDEX = 0x1900;
        public const int OSMESA_RGBA = 0x1908;
        public const int OSMESA_BGRA = 0x1;
        public const int OSMESA_ARGB = 0x2;
        public const int OSMESA_RGB = 0x1907;
        public const int OSMESA_BGR = 0x80E0;
        public const int OSMESA_RGB_565 = 0x3;

        // OSMesa data types
        public const int GL_UNSIGNED_BYTE = 0x1401;
        public const int GL_UNSIGNED_SHORT = 0x1403;
        public const int GL_UNSIGNED_SHORT_5_6_5 = 0x8363;
        public const int GL_FLOAT = 0x1406;

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OSMesaCreateContext(int format, IntPtr sharelist);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OSMesaCreateContextExt(
            int format,
            int depthBits,
            int stencilBits,
            int accumBits,
            IntPtr sharelist);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OSMesaDestroyContext(IntPtr ctx);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OSMesaMakeCurrent(
            IntPtr ctx,
            IntPtr buffer,
            int type,
            int width,
            int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OSMesaGetCurrentContext();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OSMesaGetProcAddress(
            [MarshalAs(UnmanagedType.LPStr)] string funcName);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void OSMesaPixelStore(int pname, int value);

        // Additional attribute constants for context creation
        public const int OSMESA_DEPTH_BITS = 0x30;
        public const int OSMESA_STENCIL_BITS = 0x31;
        public const int OSMESA_ACCUM_BITS = 0x32;
    }
}
