using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model
{
    internal class WasmModule
    {
        public byte[] Magic { get; set; } = [0x00, 0x61, 0x73, 0x6D];

        public uint Version { get; set; } = 1;

        public List<WasmSection> Sections { get; set; } = new List<WasmSection>();
    }
}
