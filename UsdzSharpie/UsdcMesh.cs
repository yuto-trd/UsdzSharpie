using System;
using System.Collections.Generic;

namespace UsdzSharpie
{
    /// <summary>
    /// Represents a mesh with geometry data extracted from USDZ
    /// </summary>
    public class UsdcMesh
    {
        public string Name { get; set; }

        // Geometry data
        public Vec3f[] Vertices { get; set; }
        public Vec3f[] Normals { get; set; }
        public Vec2f[] TexCoords { get; set; }
        public int[] TexCoordIndices { get; set; } // Indices for faceVarying texture coordinates
        public Vec3i[] FaceVertexIndices { get; set; } // Triangulated indices (deprecated, use RawFaceVertexIndices)
        public int[] RawFaceVertexIndices { get; set; } // Raw indices from USD file
        public int[] FaceVertexCounts { get; set; } // Number of vertices per face

        // Material reference
        public string MaterialPath { get; set; }

        // Additional mesh properties
        public string SubdivisionScheme { get; set; }
        public UsdcField.Orientation Orientation { get; set; }
        public bool DoubleSided { get; set; }

        public UsdcMesh()
        {
            Vertices = new Vec3f[0];
            Normals = new Vec3f[0];
            TexCoords = new Vec2f[0];
            FaceVertexIndices = new Vec3i[0];
            FaceVertexCounts = new int[0];
            Orientation = UsdcField.Orientation.OrientationRightHanded;
            DoubleSided = false;
        }

        /// <summary>
        /// Get triangulated indices - converts quads and polygons to triangles
        /// </summary>
        public int[] GetTriangulatedIndices()
        {
            var indices = new List<int>();

            if (RawFaceVertexIndices != null && FaceVertexCounts != null)
            {
                int indexOffset = 0;
                foreach (var faceVertexCount in FaceVertexCounts)
                {
                    if (faceVertexCount == 3)
                    {
                        // Triangle - add directly
                        indices.Add(RawFaceVertexIndices[indexOffset]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 1]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 2]);
                    }
                    else if (faceVertexCount == 4)
                    {
                        // Quad - split into two triangles
                        indices.Add(RawFaceVertexIndices[indexOffset]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 1]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 2]);

                        indices.Add(RawFaceVertexIndices[indexOffset]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 2]);
                        indices.Add(RawFaceVertexIndices[indexOffset + 3]);
                    }
                    else if (faceVertexCount > 4)
                    {
                        // N-gon - fan triangulation from first vertex
                        for (int i = 1; i < faceVertexCount - 1; i++)
                        {
                            indices.Add(RawFaceVertexIndices[indexOffset]);
                            indices.Add(RawFaceVertexIndices[indexOffset + i]);
                            indices.Add(RawFaceVertexIndices[indexOffset + i + 1]);
                        }
                    }

                    indexOffset += faceVertexCount;
                }
            }
            else if (FaceVertexIndices != null)
            {
                // Fallback to old method
                foreach (var face in FaceVertexIndices)
                {
                    indices.Add(face.X);
                    indices.Add(face.Y);
                    indices.Add(face.Z);
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// Compute normals if they don't exist
        /// </summary>
        public void ComputeNormals()
        {
            if (Vertices == null || Vertices.Length == 0)
                return;

            var normals = new Vec3f[Vertices.Length];

            if (RawFaceVertexIndices != null && FaceVertexCounts != null)
            {
                // Use raw indices and face counts
                int indexOffset = 0;
                foreach (var faceVertexCount in FaceVertexCounts)
                {
                    if (faceVertexCount >= 3)
                    {
                        // Calculate normal for this face (using first 3 vertices)
                        var idx0 = RawFaceVertexIndices[indexOffset];
                        var idx1 = RawFaceVertexIndices[indexOffset + 1];
                        var idx2 = RawFaceVertexIndices[indexOffset + 2];

                        if (idx0 < Vertices.Length && idx1 < Vertices.Length && idx2 < Vertices.Length)
                        {
                            var v0 = Vertices[idx0];
                            var v1 = Vertices[idx1];
                            var v2 = Vertices[idx2];

                            // Calculate edge vectors
                            var edge1 = new Vec3f(v1.X - v0.X, v1.Y - v0.Y, v1.Z - v0.Z);
                            var edge2 = new Vec3f(v2.X - v0.X, v2.Y - v0.Y, v2.Z - v0.Z);

                            // Cross product for normal
                            var normal = new Vec3f(
                                edge1.Y * edge2.Z - edge1.Z * edge2.Y,
                                edge1.Z * edge2.X - edge1.X * edge2.Z,
                                edge1.X * edge2.Y - edge1.Y * edge2.X
                            );

                            // Accumulate normals for all vertices in this face
                            for (int i = 0; i < faceVertexCount; i++)
                            {
                                var idx = RawFaceVertexIndices[indexOffset + i];
                                if (idx < Vertices.Length)
                                {
                                    normals[idx] = new Vec3f(
                                        normals[idx].X + normal.X,
                                        normals[idx].Y + normal.Y,
                                        normals[idx].Z + normal.Z
                                    );
                                }
                            }
                        }
                    }

                    indexOffset += faceVertexCount;
                }
            }
            else if (FaceVertexIndices != null)
            {
                // Fallback to old method
                foreach (var face in FaceVertexIndices)
                {
                    if (face.X >= Vertices.Length || face.Y >= Vertices.Length || face.Z >= Vertices.Length)
                        continue;

                    var v0 = Vertices[face.X];
                    var v1 = Vertices[face.Y];
                    var v2 = Vertices[face.Z];

                    var edge1 = new Vec3f(v1.X - v0.X, v1.Y - v0.Y, v1.Z - v0.Z);
                    var edge2 = new Vec3f(v2.X - v0.X, v2.Y - v0.Y, v2.Z - v0.Z);

                    var normal = new Vec3f(
                        edge1.Y * edge2.Z - edge1.Z * edge2.Y,
                        edge1.Z * edge2.X - edge1.X * edge2.Z,
                        edge1.X * edge2.Y - edge1.Y * edge2.X
                    );

                    normals[face.X] = new Vec3f(normals[face.X].X + normal.X, normals[face.X].Y + normal.Y, normals[face.X].Z + normal.Z);
                    normals[face.Y] = new Vec3f(normals[face.Y].X + normal.X, normals[face.Y].Y + normal.Y, normals[face.Y].Z + normal.Z);
                    normals[face.Z] = new Vec3f(normals[face.Z].X + normal.X, normals[face.Z].Y + normal.Y, normals[face.Z].Z + normal.Z);
                }
            }

            // Normalize all normals
            for (int i = 0; i < normals.Length; i++)
            {
                var n = normals[i];
                var length = (float)Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
                if (length > 0.0001f)
                {
                    normals[i] = new Vec3f(n.X / length, n.Y / length, n.Z / length);
                }
            }

            Normals = normals;
        }
    }
}
