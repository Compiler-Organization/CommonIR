using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmGlobalSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Global;

        public ulong Size { get; set; } = 0;
    }
}
