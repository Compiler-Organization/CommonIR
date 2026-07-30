using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmCustomSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Custom;

        public ulong Size { get; set; } = 0;
    }
}
