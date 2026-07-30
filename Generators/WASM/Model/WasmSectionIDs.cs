using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model
{
    internal enum WasmSectionIDs
    {
        Custom = 0x00,
        Type = 0x01,
        Import = 0x02,
        Function = 0x03,
        Table = 0x04,
        Memory = 0x05,
        Global = 0x06,
        Export = 0x07,
        Start = 0x08,
        Element = 0x09,
        Code = 0x0A,
        Data = 0x0B,
    }
}
