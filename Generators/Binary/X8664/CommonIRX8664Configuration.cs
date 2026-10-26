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

        public CommonIRX8664Configuration()
        {

        }
    }
}
