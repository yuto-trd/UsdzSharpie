using System;

namespace UsdzSharpie
{
    // Vector types
    public struct Vec2i
    {
        public int X, Y;
        public Vec2i(int x, int y) { X = x; Y = y; }
    }

    public struct Vec2f
    {
        public float X, Y;
        public Vec2f(float x, float y) { X = x; Y = y; }
    }

    public struct Vec2d
    {
        public double X, Y;
        public Vec2d(double x, double y) { X = x; Y = y; }
    }

    public struct Vec2h
    {
        public ushort X, Y; // Half precision
        public Vec2h(ushort x, ushort y) { X = x; Y = y; }
    }

    public struct Vec3i
    {
        public int X, Y, Z;
        public Vec3i(int x, int y, int z) { X = x; Y = y; Z = z; }
    }

    public struct Vec3f
    {
        public float X, Y, Z;
        public Vec3f(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    public struct Vec3d
    {
        public double X, Y, Z;
        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    public struct Vec3h
    {
        public ushort X, Y, Z; // Half precision
        public Vec3h(ushort x, ushort y, ushort z) { X = x; Y = y; Z = z; }
    }

    public struct Vec4i
    {
        public int X, Y, Z, W;
        public Vec4i(int x, int y, int z, int w) { X = x; Y = y; Z = z; W = w; }
    }

    public struct Vec4f
    {
        public float X, Y, Z, W;
        public Vec4f(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    }

    public struct Vec4d
    {
        public double X, Y, Z, W;
        public Vec4d(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
    }

    public struct Vec4h
    {
        public ushort X, Y, Z, W; // Half precision
        public Vec4h(ushort x, ushort y, ushort z, ushort w) { X = x; Y = y; Z = z; W = w; }
    }

    // Matrix types
    public struct Matrix2d
    {
        public double M00, M01;
        public double M10, M11;
    }

    public struct Matrix3d
    {
        public double M00, M01, M02;
        public double M10, M11, M12;
        public double M20, M21, M22;
    }

    public struct Matrix4d
    {
        public double M00, M01, M02, M03;
        public double M10, M11, M12, M13;
        public double M20, M21, M22, M23;
        public double M30, M31, M32, M33;

        public static Matrix4d Identity => new Matrix4d
        {
            M00 = 1, M01 = 0, M02 = 0, M03 = 0,
            M10 = 0, M11 = 1, M12 = 0, M13 = 0,
            M20 = 0, M21 = 0, M22 = 1, M23 = 0,
            M30 = 0, M31 = 0, M32 = 0, M33 = 1
        };
    }

    // Quaternion types
    public struct Quatd
    {
        public double X, Y, Z, W;
        public Quatd(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }
    }

    public struct Quatf
    {
        public float X, Y, Z, W;
        public Quatf(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
    }

    public struct Quath
    {
        public ushort X, Y, Z, W; // Half precision
        public Quath(ushort x, ushort y, ushort z, ushort w) { X = x; Y = y; Z = z; W = w; }
    }
}
