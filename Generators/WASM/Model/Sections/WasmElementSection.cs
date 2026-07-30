using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmElementSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Element;

        public ulong Size { get; set; } = 0;
    }
}
