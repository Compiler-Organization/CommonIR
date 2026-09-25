using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL
{
    public class CommonIRHLSLConfiguration : CommonIRTargetConfiguration
    {
        /// <summary>
        /// Defaults to <see cref="HLSLShaderStage.Compute"/>
        /// </summary>
        public HLSLShaderStage ShaderStage { get; set; } = HLSLShaderStage.Compute;

        /// <summary>
        /// Defaults to "cs_6_0"
        /// </summary>
        public string ShaderTarget { get; set; } = "cs_6_0";

        /// <summary>
        /// Defaults to "main"
        /// </summary>
        public string EntryPointName { get; set; } = "main";

        /// <summary>
        /// Thread execution dimensions used automatically if emitting a Compute Shader.
        /// </summary>
        public (int X, int Y, int Z) ThreadGroupSizes { get; set; } = (64, 1, 1);

        /// <summary>
        /// Defaults to <see cref="HLSLCompilationType.StandaloneFunctions"/>
        /// </summary>
        public HLSLCompilationType CompilationType = HLSLCompilationType.StandaloneFunctions;

        public CommonIRHLSLConfiguration()
        {
            
        }
    }

    public enum HLSLShaderStage
    {
        Compute,
        Vertex,
        Pixel
    }

    public enum HLSLCompilationType
    {
        /// <summary>
        /// Outputs one file per exported entry-point, bundling child helper functions inside it.
        /// </summary>
        ModulePerExport,

        /// <summary>
        /// Explodes every single function (internal or exported) into its own standalone file.
        /// </summary>
        StandaloneFunctions,
    }
}
