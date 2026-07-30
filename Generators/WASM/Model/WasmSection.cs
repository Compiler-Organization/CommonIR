using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model
{
    internal interface WasmSection
    {
        public required WasmSectionIDs ID { get; }

        public ulong Size { get; set; }
    }
}
