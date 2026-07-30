using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmFunctionSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Function;

        public ulong Size { get; set; } = 0;
    }
}
