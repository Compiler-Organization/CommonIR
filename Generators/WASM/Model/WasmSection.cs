using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model
{
    internal interface WasmSection
    {
        public WasmSectionIDs ID { get; }

        public ulong Size { get; set; }

        /// <summary>
        /// Converts the section to its binary variant
        /// </summary>
        /// <returns></returns>
        public byte[] Serialize();
    }
}
