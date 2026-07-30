using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmStartSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Start;

        public ulong Size { get; set; } = 0;
    }
}
