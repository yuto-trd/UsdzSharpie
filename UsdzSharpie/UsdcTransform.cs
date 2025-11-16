using System;

namespace UsdzSharpie
{
    /// <summary>
    /// Represents transformation data for a scene node
    /// </summary>
    public class UsdcTransform
    {
        public Matrix4d Matrix { get; set; }
        public Vec3d Translation { get; set; }
        public Vec3d Rotation { get; set; } // Euler angles in degrees
        public Vec3d Scale { get; set; }
        public Quatd RotationQuat { get; set; }

        public bool HasMatrix { get; set; }
        public bool HasTRS { get; set; } // Translation, Rotation, Scale

        public UsdcTransform()
        {
            Matrix = Matrix4d.Identity;
            Translation = new Vec3d(0, 0, 0);
            Rotation = new Vec3d(0, 0, 0);
            Scale = new Vec3d(1, 1, 1);
            RotationQuat = new Quatd(0, 0, 0, 1);
            HasMatrix = false;
            HasTRS = false;
        }

        /// <summary>
        /// Convert to a 4x4 transformation matrix
        /// </summary>
        public Matrix4d ToMatrix()
        {
            if (HasMatrix)
            {
                return Matrix;
            }

            if (HasTRS)
            {
                return CreateTRSMatrix(Translation, Scale, RotationQuat);
            }

            return Matrix4d.Identity;
        }

        /// <summary>
        /// Create a transformation matrix from Translation, Rotation (quaternion), and Scale
        /// </summary>
        private static Matrix4d CreateTRSMatrix(Vec3d translation, Vec3d scale, Quatd rotation)
        {
            // Convert quaternion to rotation matrix
            double xx = rotation.X * rotation.X;
            double yy = rotation.Y * rotation.Y;
            double zz = rotation.Z * rotation.Z;
            double xy = rotation.X * rotation.Y;
            double xz = rotation.X * rotation.Z;
            double yz = rotation.Y * rotation.Z;
            double wx = rotation.W * rotation.X;
            double wy = rotation.W * rotation.Y;
            double wz = rotation.W * rotation.Z;

            Matrix4d result = new Matrix4d();

            // Rotation and scale
            result.M00 = (1 - 2 * (yy + zz)) * scale.X;
            result.M01 = (2 * (xy - wz)) * scale.X;
            result.M02 = (2 * (xz + wy)) * scale.X;
            result.M03 = 0;

            result.M10 = (2 * (xy + wz)) * scale.Y;
            result.M11 = (1 - 2 * (xx + zz)) * scale.Y;
            result.M12 = (2 * (yz - wx)) * scale.Y;
            result.M13 = 0;

            result.M20 = (2 * (xz - wy)) * scale.Z;
            result.M21 = (2 * (yz + wx)) * scale.Z;
            result.M22 = (1 - 2 * (xx + yy)) * scale.Z;
            result.M23 = 0;

            // Translation
            result.M30 = translation.X;
            result.M31 = translation.Y;
            result.M32 = translation.Z;
            result.M33 = 1;

            return result;
        }

        /// <summary>
        /// Convert Euler angles to quaternion
        /// </summary>
        public static Quatd EulerToQuaternion(Vec3d euler)
        {
            // Convert degrees to radians
            double x = euler.X * Math.PI / 180.0;
            double y = euler.Y * Math.PI / 180.0;
            double z = euler.Z * Math.PI / 180.0;

            double cx = Math.Cos(x * 0.5);
            double sx = Math.Sin(x * 0.5);
            double cy = Math.Cos(y * 0.5);
            double sy = Math.Sin(y * 0.5);
            double cz = Math.Cos(z * 0.5);
            double sz = Math.Sin(z * 0.5);

            Quatd q = new Quatd();
            q.W = cx * cy * cz + sx * sy * sz;
            q.X = sx * cy * cz - cx * sy * sz;
            q.Y = cx * sy * cz + sx * cy * sz;
            q.Z = cx * cy * sz - sx * sy * cz;

            return q;
        }
    }
}
