using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmCodeSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Code;

        public ulong Size { get; set; } = 0;
    }
}
