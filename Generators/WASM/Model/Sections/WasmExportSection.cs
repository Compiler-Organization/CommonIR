using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmExportSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Export;

        public ulong Size { get; set; } = 0;
    }
}
