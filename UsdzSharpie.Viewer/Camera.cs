using OpenTK.Mathematics;

namespace UsdzSharpie.Viewer
{
    public class Camera
    {
        public Vector3 Position { get; set; }
        public Vector3 Front { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Right { get; set; }
        public Vector3 WorldUp { get; set; }

        public float Yaw { get; set; }
        public float Pitch { get; set; }

        public float Fov { get; set; }
        public float AspectRatio { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }

        public Camera(Vector3 position, float aspectRatio)
        {
            Position = position;
            WorldUp = Vector3.UnitY;
            Yaw = -90.0f;
            Pitch = 0.0f;
            Fov = 45.0f;
            AspectRatio = aspectRatio;
            Near = 0.1f;
            Far = 100.0f;

            Front = -Vector3.UnitZ;
            UpdateCameraVectors();
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Front, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                AspectRatio,
                Near,
                Far);
        }

        private void UpdateCameraVectors()
        {
            Vector3 front;
            front.X = (float)(System.Math.Cos(MathHelper.DegreesToRadians(Yaw)) * System.Math.Cos(MathHelper.DegreesToRadians(Pitch)));
            front.Y = (float)System.Math.Sin(MathHelper.DegreesToRadians(Pitch));
            front.Z = (float)(System.Math.Sin(MathHelper.DegreesToRadians(Yaw)) * System.Math.Cos(MathHelper.DegreesToRadians(Pitch)));
            Front = Vector3.Normalize(front);

            Right = Vector3.Normalize(Vector3.Cross(Front, WorldUp));
            Up = Vector3.Normalize(Vector3.Cross(Right, Front));
        }
    }
}
