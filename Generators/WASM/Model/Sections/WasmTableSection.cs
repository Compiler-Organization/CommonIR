using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmTableSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Table;

        public ulong Size { get; set; } = 0;
    }
}
