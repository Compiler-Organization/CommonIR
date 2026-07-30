using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmDataSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Data;

        public ulong Size { get; set; } = 0;
    }
}
