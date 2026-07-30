using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmTypeSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Type;

        public ulong Size { get; set; } = 0;
    }
}
