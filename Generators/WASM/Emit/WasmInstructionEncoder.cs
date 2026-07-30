using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmInstructionEncoder
    {
        public enum Mnemonics
        {
            i32_const = 0x41,
            i32_add = 0x6A,

            call = 0x10,
            end = 0x0B,
        }
    }
}
