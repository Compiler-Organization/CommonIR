using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmMemorySection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Memory;

        public ulong Size { get; set; } = 0;
    }
}
