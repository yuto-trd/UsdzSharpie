using System.Collections.Generic;
using System.Linq;

namespace UsdzSharpie
{
    /// <summary>
    /// Represents a complete scene extracted from USDZ file
    /// </summary>
    public class UsdcScene
    {
        public UsdcSceneNode RootNode { get; set; }
        public List<UsdcSceneNode> AllNodes { get; set; }
        public Dictionary<string, UsdcMesh> Meshes { get; set; }
        public Dictionary<string, UsdcMaterial> Materials { get; set; }
        public Dictionary<string, UsdcShader> Shaders { get; set; }

        public UsdcScene()
        {
            AllNodes = new List<UsdcSceneNode>();
            Meshes = new Dictionary<string, UsdcMesh>();
            Materials = new Dictionary<string, UsdcMaterial>();
            Shaders = new Dictionary<string, UsdcShader>();
        }

        /// <summary>
        /// Get all mesh nodes in the scene
        /// </summary>
        public List<UsdcSceneNode> GetMeshNodes()
        {
            return AllNodes.Where(n => n.NodeType == UsdcNode.NodeType.NODE_TYPE_GEOM_MESH).ToList();
        }

        /// <summary>
        /// Get all material nodes in the scene
        /// </summary>
        public List<UsdcSceneNode> GetMaterialNodes()
        {
            return AllNodes.Where(n => n.NodeType == UsdcNode.NodeType.NODE_TYPE_MATERIAL).ToList();
        }
    }

    /// <summary>
    /// Represents a node in the scene hierarchy with extracted data
    /// </summary>
    public class UsdcSceneNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public UsdcNode.NodeType NodeType { get; set; }

        public UsdcSceneNode Parent { get; set; }
        public List<UsdcSceneNode> Children { get; set; }

        // Transform
        public UsdcTransform Transform { get; set; }

        // Mesh reference (for geometry nodes)
        public UsdcMesh Mesh { get; set; }

        // Material reference (for material nodes)
        public UsdcMaterial Material { get; set; }

        // Shader reference (for shader nodes)
        public UsdcShader Shader { get; set; }

        // Field values (raw data from USDZ)
        public Dictionary<string, object> Fields { get; set; }

        public UsdcSceneNode()
        {
            Children = new List<UsdcSceneNode>();
            Transform = new UsdcTransform();
            Fields = new Dictionary<string, object>();
        }

        /// <summary>
        /// Get world transformation matrix by combining parent transforms
        /// </summary>
        public Matrix4d GetWorldTransform()
        {
            if (Parent == null)
            {
                return Transform.ToMatrix();
            }

            // Combine with parent transform
            return MultiplyMatrices(Parent.GetWorldTransform(), Transform.ToMatrix());
        }

        private Matrix4d MultiplyMatrices(Matrix4d a, Matrix4d b)
        {
            Matrix4d result = new Matrix4d();

            result.M00 = a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20 + a.M03 * b.M30;
            result.M01 = a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21 + a.M03 * b.M31;
            result.M02 = a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22 + a.M03 * b.M32;
            result.M03 = a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03 * b.M33;

            result.M10 = a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20 + a.M13 * b.M30;
            result.M11 = a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31;
            result.M12 = a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32;
            result.M13 = a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33;

            result.M20 = a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20 + a.M23 * b.M30;
            result.M21 = a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31;
            result.M22 = a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32;
            result.M23 = a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33;

            result.M30 = a.M30 * b.M00 + a.M31 * b.M10 + a.M32 * b.M20 + a.M33 * b.M30;
            result.M31 = a.M30 * b.M01 + a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31;
            result.M32 = a.M30 * b.M02 + a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32;
            result.M33 = a.M30 * b.M03 + a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33;

            return result;
        }
    }
}
