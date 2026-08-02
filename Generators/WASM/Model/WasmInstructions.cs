namespace CommonIR.Generators.WASM.Model
{
    internal enum WasmInstructions : byte
    {
        Block = 0x02,
        Call = 0x10,
        End = 0x0B,
        Br = 0x0C,
        Br_if = 0x0D,
        Br_table = 0x0E,
        Return = 0x0F,

        Local_get = 0x20,

        I32_const = 0x41,

        I32_add = 0x6A,
    }
}
