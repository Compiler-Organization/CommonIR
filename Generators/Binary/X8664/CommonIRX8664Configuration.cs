using CommonIR.Generators.Textual.HLSL;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664
{
    public class CommonIRX8664Configuration : CommonIRTargetConfiguration
    {
        public string? DXCPath { get; set; }

        public CommonIRHLSLConfiguration Shaders { get; set; } = new CommonIRHLSLConfiguration();

        public bool EmitShaderSources { get; set; }

        public ushort Subsystem { get; set; } = 3;

        public ulong StackReserve { get; set; } = 0x800000;

        /// <summary>
        /// Versions counted loops so that their array accesses are checked once before the loop, and vectorizes
        /// element-wise floating point loops. Set by the code generator when optimizing.
        /// </summary>
        public bool OptimizeLoops { get; set; }

        public CommonIRX8664Configuration()
        {

        }
    }
}
