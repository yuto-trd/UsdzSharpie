using System;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using UsdzSharpie;

namespace UsdzSharpie.Viewer
{
    public class MeshRenderer : IDisposable
    {
        private int vao;
        private int vbo;
        private int ebo;
        private int vertexCount;
        private int indexCount;

        public Matrix4d Transform { get; set; }
        public Vector3 Color { get; set; }
        public Texture DiffuseTexture { get; set; }

        public MeshRenderer(UsdcMesh mesh)
        {
            Transform = Matrix4d.Identity;
            Color = new Vector3(0.8f, 0.8f, 0.8f);

            if (mesh.Vertices == null || mesh.Vertices.Length == 0)
            {
                Console.WriteLine("Warning: Mesh has no vertices");
                return;
            }

            // Compute normals if they don't exist or if they're faceVarying (count doesn't match vertex count)
            if (mesh.Normals == null || mesh.Normals.Length == 0 || mesh.Normals.Length != mesh.Vertices.Length)
            {
                Console.WriteLine($"Computing normals (had {mesh.Normals?.Length ?? 0}, need {mesh.Vertices.Length})");
                mesh.ComputeNormals();
            }

            // Create vertex data (position + normal + texcoord)
            var hasNormals = mesh.Normals != null && mesh.Normals.Length > 0;
            var hasTexCoords = mesh.TexCoords != null && mesh.TexCoords.Length > 0;

            var stride = 8; // 3 (pos) + 3 (normal) + 2 (texcoord)
            var vertexData = new float[mesh.Vertices.Length * stride];

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                vertexData[i * stride + 0] = v.X;
                vertexData[i * stride + 1] = v.Y;
                vertexData[i * stride + 2] = v.Z;

                if (hasNormals && i < mesh.Normals.Length)
                {
                    var n = mesh.Normals[i];
                    vertexData[i * stride + 3] = n.X;
                    vertexData[i * stride + 4] = n.Y;
                    vertexData[i * stride + 5] = n.Z;
                }
                else
                {
                    vertexData[i * stride + 3] = 0.0f;
                    vertexData[i * stride + 4] = 1.0f;
                    vertexData[i * stride + 5] = 0.0f;
                }

                if (hasTexCoords && i < mesh.TexCoords.Length)
                {
                    var t = mesh.TexCoords[i];
                    vertexData[i * stride + 6] = t.X;
                    vertexData[i * stride + 7] = t.Y;
                }
                else
                {
                    vertexData[i * stride + 6] = 0.0f;
                    vertexData[i * stride + 7] = 0.0f;
                }
            }

            vertexCount = mesh.Vertices.Length;

            // Debug: Check texture coordinates
            if (hasTexCoords && mesh.TexCoords.Length > 0)
            {
                Console.WriteLine($"First 3 TexCoords: ({mesh.TexCoords[0].X}, {mesh.TexCoords[0].Y}), " +
                    $"({mesh.TexCoords[Math.Min(1, mesh.TexCoords.Length - 1)].X}, {mesh.TexCoords[Math.Min(1, mesh.TexCoords.Length - 1)].Y}), " +
                    $"({mesh.TexCoords[Math.Min(2, mesh.TexCoords.Length - 1)].X}, {mesh.TexCoords[Math.Min(2, mesh.TexCoords.Length - 1)].Y})");
            }
            else
            {
                Console.WriteLine("No texture coordinates available!");
            }

            // Create VAO
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            // Create VBO
            vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexData.Length * sizeof(float), vertexData, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normal attribute
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // TexCoord attribute
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Create index buffer if available
            var indices = mesh.GetTriangulatedIndices();
            if (indices != null && indices.Length > 0)
            {
                indexCount = indices.Length;

                ebo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(int), indices, BufferUsageHint.StaticDraw);
            }
            else
            {
                indexCount = 0;
            }

            GL.BindVertexArray(0);

            Console.WriteLine($"MeshRenderer created: {vertexCount} vertices, {indexCount} indices");
        }

        public void Draw(Shader shader)
        {
            if (vao == 0) return;

            // Convert Matrix4d to Matrix4 for OpenGL
            var transform = new Matrix4(
                (float)Transform.M00, (float)Transform.M01, (float)Transform.M02, (float)Transform.M03,
                (float)Transform.M10, (float)Transform.M11, (float)Transform.M12, (float)Transform.M13,
                (float)Transform.M20, (float)Transform.M21, (float)Transform.M22, (float)Transform.M23,
                (float)Transform.M30, (float)Transform.M31, (float)Transform.M32, (float)Transform.M33
            );

            shader.SetMatrix4("model", transform);
            shader.SetVector3("objectColor", Color);

            // Bind texture if available
            if (DiffuseTexture != null)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                DiffuseTexture.Use(TextureUnit.Texture0);
                shader.SetInt("diffuseTexture", 0);
                shader.SetBool("hasTexture", true);
            }
            else
            {
                // Explicitly unbind texture and disable texture usage
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, 0);
                shader.SetBool("hasTexture", false);
            }

            GL.BindVertexArray(vao);

            if (indexCount > 0)
            {
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
            }
            else
            {
                GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            }

            GL.BindVertexArray(0);
        }

        public void Dispose()
        {
            if (vao != 0)
            {
                GL.DeleteVertexArray(vao);
                vao = 0;
            }

            if (vbo != 0)
            {
                GL.DeleteBuffer(vbo);
                vbo = 0;
            }

            if (ebo != 0)
            {
                GL.DeleteBuffer(ebo);
                ebo = 0;
            }
        }
    }
}
