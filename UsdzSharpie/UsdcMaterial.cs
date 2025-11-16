using System.Collections.Generic;

namespace UsdzSharpie
{
    /// <summary>
    /// Represents material information extracted from USDZ
    /// </summary>
    public class UsdcMaterial
    {
        public string Name { get; set; }
        public string Path { get; set; }

        // PBR Material properties
        public Vec3f DiffuseColor { get; set; }
        public Vec3f EmissiveColor { get; set; }
        public Vec3f SpecularColor { get; set; }
        public float Metallic { get; set; }
        public float Roughness { get; set; }
        public float Opacity { get; set; }
        public float IOR { get; set; } // Index of Refraction

        // Texture references
        public string DiffuseTexture { get; set; }
        public string NormalTexture { get; set; }
        public string MetallicTexture { get; set; }
        public string RoughnessTexture { get; set; }
        public string EmissiveTexture { get; set; }
        public string OcclusionTexture { get; set; }

        // Shader inputs (generic storage for all shader parameters)
        public Dictionary<string, object> ShaderInputs { get; set; }

        public UsdcMaterial()
        {
            DiffuseColor = new Vec3f(0.8f, 0.8f, 0.8f);
            EmissiveColor = new Vec3f(0, 0, 0);
            SpecularColor = new Vec3f(1, 1, 1);
            Metallic = 0.0f;
            Roughness = 0.5f;
            Opacity = 1.0f;
            IOR = 1.5f;
            ShaderInputs = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Represents a shader node in the material network
    /// </summary>
    public class UsdcShader
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string ShaderId { get; set; } // e.g., "UsdPreviewSurface", "UsdUVTexture"

        public Dictionary<string, object> Inputs { get; set; }
        public Dictionary<string, string> Outputs { get; set; }

        public UsdcShader()
        {
            Inputs = new Dictionary<string, object>();
            Outputs = new Dictionary<string, string>();
        }
    }
}
