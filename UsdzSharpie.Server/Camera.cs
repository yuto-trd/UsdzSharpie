using OpenTK.Mathematics;

namespace UsdzSharpie.Server
{
    public class Camera
    {
        public Vector3 Position { get; set; }
        public Vector3 Target { get; set; }
        public Vector3 Up { get; set; }

        public float Fov { get; set; }
        public float AspectRatio { get; set; }
        public float Near { get; set; }
        public float Far { get; set; }

        public Camera(Vector3 position, Vector3 target, float aspectRatio, float fov = 45.0f)
        {
            Position = position;
            Target = target;
            Up = Vector3.UnitY;
            Fov = fov;
            AspectRatio = aspectRatio;
            Near = 0.1f;
            Far = 100.0f;
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Target, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(Fov),
                AspectRatio,
                Near,
                Far);
        }
    }
}
