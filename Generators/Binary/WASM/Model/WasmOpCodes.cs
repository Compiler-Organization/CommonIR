namespace CommonIR.Generators.Binary.WASM.Model
{
    internal enum WasmOpCodes : byte
    {
        // Blocks

        Unreachable = 0x00,
        Nop = 0x01,
        Block = 0x02,
        Loop = 0x03,
        If = 0x04,
        Else = 0x05,
        End = 0x0B,


        Call = 0x10,
        Br = 0x0C,
        Br_if = 0x0D,
        Br_table = 0x0E,
        Return = 0x0F,

        // Managing memory
        Memory_size = 0x3F,
        Memory_grow = 0x40,

        // Stores

        /// <summary>
        /// <para>Store 32-bit integer</para>
        /// <para>Stack transition: [i32, i32] -> []</para>
        /// <para>Pops a base address and an i32 value, then writes 4 bytes into linear memory.</para>
        /// </summary>
        I32_store = 0x36,

        /// <summary>
        /// <para>Store 64-bit integer</para>
        /// <para>Stack transition: [i32, i64] -> []</para>
        /// <para>Pops a base address and an i64 value, then writes 8 bytes into linear memory.</para>
        /// </summary>
        I64_store = 0x37,

        /// <summary>
        /// <para>Store 32-bit float</para>
        /// <para>Stack transition: [i32, f32] -> []</para>
        /// <para>Pops a base address and an f32 value, then writes 4 bytes into linear memory.</para>
        /// </summary>
        F32_store = 0x38,

        /// <summary>
        /// <para>Store 64-bit float</para>
        /// <para>Stack transition: [i32, f64] -> []</para>
        /// <para>Pops a base address and an f64 value, then writes 8 bytes into linear memory.</para>
        /// </summary>
        F64_store = 0x39,

        /// <summary>
        /// <para>Store i32 as 8-bit integer</para>
        /// <para>Stack transition: [i32, i32] -> []</para>
        /// <para>Pops a base address and an i32 value, wrapping it to write 1 byte into linear memory.</para>
        /// </summary>
        I32_store8 = 0x3A,

        /// <summary>
        /// <para>Store i32 as 16-bit integer</para>
        /// <para>Stack transition: [i32, i32] -> []</para>
        /// <para>Pops a base address and an i32 value, wrapping it to write 2 bytes into linear memory.</para>
        /// </summary>
        I32_store16 = 0x3B,

        /// <summary>
        /// <para>Store i64 as 8-bit integer</para>
        /// <para>Stack transition: [i32, i64] -> []</para>
        /// <para>Pops a base address and an i64 value, wrapping it to write 1 byte into linear memory.</para>
        /// </summary>
        I64_store8 = 0x3C,

        /// <summary>
        /// <para>Store i64 as 16-bit integer</para>
        /// <para>Stack transition: [i32, i64] -> []</para>
        /// <para>Pops a base address and an i64 value, wrapping it to write 2 bytes into linear memory.</para>
        /// </summary>
        I64_store16 = 0x3D,

        /// <summary>
        /// <para>Store i64 as 32-bit integer</para>
        /// <para>Stack transition: [i32, i64] -> []</para>
        /// <para>Pops a base address and an i64 value, wrapping it to write 4 bytes into linear memory.</para>
        /// </summary>
        I64_store32 = 0x3E,

        // Loads

        /// <summary>
        /// <para>Load 32-bit integer</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a base address, reads 4 bytes from memory, and pushes the i32 value.</para>
        /// </summary>
        I32_load = 0x28,

        /// <summary>
        /// <para>Load 64-bit integer</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 8 bytes from memory, and pushes the i64 value.</para>
        /// </summary>
        I64_load = 0x29,

        /// <summary>
        /// <para>Load 32-bit float</para>
        /// <para>Stack transition: [i32] -> [f32]</para>
        /// <para>Pops a base address, reads 4 bytes from memory, and pushes the f32 value.</para>
        /// </summary>
        F32_load = 0x2A,

        /// <summary>
        /// <para>Load 64-bit float</para>
        /// <para>Stack transition: [i32] -> [f64]</para>
        /// <para>Pops a base address, reads 8 bytes from memory, and pushes the f64 value.</para>
        /// </summary>
        F64_load = 0x2B,

        /// <summary>
        /// <para>Load 8-bit integer (sign-extended to i32)</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a base address, reads 1 byte from memory, sign-extends it, and pushes the i32 result.</para>
        /// </summary>
        I32_load8_s = 0x2C,

        /// <summary>
        /// <para>Load 8-bit integer (zero-extended to i32)</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a base address, reads 1 byte from memory, zero-extends it, and pushes the i32 result.</para>
        /// </summary>
        I32_load8_u = 0x2D,

        /// <summary>
        /// <para>Load 16-bit integer (sign-extended to i32)</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a base address, reads 2 bytes from memory, sign-extends it, and pushes the i32 result.</para>
        /// </summary>
        I32_load16_s = 0x2E,

        /// <summary>
        /// <para>Load 16-bit integer (zero-extended to i32)</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a base address, reads 2 bytes from memory, zero-extends it, and pushes the i32 result.</para>
        /// </summary>
        I32_load16_u = 0x2F,

        /// <summary>
        /// <para>Load 8-bit integer (sign-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 1 byte from memory, sign-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load8_s = 0x30,

        /// <summary>
        /// <para>Load 8-bit integer (zero-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 1 byte from memory, zero-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load8_u = 0x31,

        /// <summary>
        /// <para>Load 16-bit integer (sign-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 2 bytes from memory, sign-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load16_s = 0x32,

        /// <summary>
        /// <para>Load 16-bit integer (zero-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 2 bytes from memory, zero-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load16_u = 0x33,

        /// <summary>
        /// <para>Load 32-bit integer (sign-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 4 bytes from memory, sign-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load32_s = 0x34,

        /// <summary>
        /// <para>Load 32-bit integer (zero-extended to i64)</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a base address, reads 4 bytes from memory, zero-extends it, and pushes the i64 result.</para>
        /// </summary>
        I64_load32_u = 0x35,


        // Locals
        Local_get = 0x20,
        Local_set = 0x21,
        Local_tee = 0x22,

        // Globals
        Global_get = 0x23,
        Global_set = 0x24,

        // Constants
        I32_const = 0x41,
        I64_const = 0x42,
        F32_const = 0x43,
        F64_const = 0x44,

        // Arithmetic

        /// <summary>
        /// <para>Add</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, adds them, and pushes the result to the stack.</para>
        /// </summary>
        I32_add = 0x6A,

        /// <summary>
        /// <para>Sub</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, subtracts them, and pushes the result to the stack.</para>
        /// </summary>
        I32_sub = 0x6B,

        /// <summary>
        /// <para>Mul</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, multiplies them, and pushes the result to the stack.</para>
        /// </summary>
        I32_mul = 0x6C,
        /// <summary>
        /// <para>Div Signed</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, performs signed division, and pushes the result to the stack.</para>
        /// </summary>
        I32_div_s = 0x6D,

        /// <summary>
        /// <para>Div Unsigned</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, performs unsigned division, and pushes the result to the stack.</para>
        /// </summary>
        I32_div_u = 0x6E,

        /// <summary>
        /// <para>Rem Signed</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, computes the signed remainder, and pushes the result to the stack.</para>
        /// </summary>
        I32_rem_s = 0x6F,

        /// <summary>
        /// <para>Rem Unsigned</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, computes the unsigned remainder, and pushes the result to the stack.</para>
        /// </summary>
        I32_rem_u = 0x70,


        /// <summary>
        /// <para>Add</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, adds them, and pushes the result to the stack.</para>
        /// </summary>
        I64_add = 0x7C,

        /// <summary>
        /// <para>Sub</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, subtracts them, and pushes the result to the stack.</para>
        /// </summary>
        I64_sub = 0x7D,

        /// <summary>
        /// <para>Mul</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, multiplies them, and pushes the result to the stack.</para>
        /// </summary>
        I64_mul = 0x7E,

        /// <summary>
        /// <para>Div Signed</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, performs signed division, and pushes the result to the stack.</para>
        /// </summary>
        I64_div_s = 0x7F,

        /// <summary>
        /// <para>Div Unsigned</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, performs unsigned division, and pushes the result to the stack.</para>
        /// </summary>
        I64_div_u = 0x80,

        /// <summary>
        /// <para>Rem Signed</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, computes the signed remainder, and pushes the result to the stack.</para>
        /// </summary>
        I64_rem_s = 0x81,

        /// <summary>
        /// <para>Rem Unsigned</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers, computes the unsigned remainder, and pushes the result to the stack.</para>
        /// </summary>
        I64_rem_u = 0x82,


        /// <summary>
        /// <para>Absolute Value</para>
        /// <para>Stack transition: [f32] -> [f32]</para>
        /// <para>Pops one float, computes its absolute value, and pushes the result to the stack.</para>
        /// </summary>
        F32_abs = 0x8B,

        /// <summary>
        /// <para>Negate</para>
        /// <para>Stack transition: [f32] -> [f32]</para>
        /// <para>Pops one float, negates its sign bit, and pushes the result to the stack.</para>
        /// </summary>
        F32_neg = 0x8C,

        /// <summary>
        /// <para>Square Root</para>
        /// <para>Stack transition: [f32] -> [f32]</para>
        /// <para>Pops one float, computes its square root, and pushes the result to the stack.</para>
        /// </summary>
        F32_sqrt = 0x91,

        /// <summary>
        /// <para>Add</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, adds them, and pushes the result to the stack.</para>
        /// </summary>
        F32_add = 0x92,

        /// <summary>
        /// <para>Sub</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, subtracts them, and pushes the result to the stack.</para>
        /// </summary>
        F32_sub = 0x93,

        /// <summary>
        /// <para>Mul</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, multiplies them, and pushes the result to the stack.</para>
        /// </summary>
        F32_mul = 0x94,

        /// <summary>
        /// <para>Div</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, divides them, and pushes the result to the stack.</para>
        /// </summary>
        F32_div = 0x95,

        /// <summary>
        /// <para>Minimum</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, computes their minimum value, and pushes the result to the stack.</para>
        /// </summary>
        F32_min = 0x96,

        /// <summary>
        /// <para>Maximum</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, computes their maximum value, and pushes the result to the stack.</para>
        /// </summary>
        F32_max = 0x97,

        /// <summary>
        /// <para>Copysign</para>
        /// <para>Stack transition: [f32, f32] -> [f32]</para>
        /// <para>Pops two floats, copies the sign of the second to the absolute value of the first, and pushes the result.</para>
        /// </summary>
        F32_copysign = 0x98,


        /// <summary>
        /// <para>Absolute Value</para>
        /// <para>Stack transition: [f64] -> [f64]</para>
        /// <para>Pops one float, computes its absolute value, and pushes the result to the stack.</para>
        /// </summary>
        F64_abs = 0x99,

        /// <summary>
        /// <para>Negate</para>
        /// <para>Stack transition: [f64] -> [f64]</para>
        /// <para>Pops one float, negates its sign bit, and pushes the result to the stack.</para>
        /// </summary>
        F64_neg = 0x9A,

        /// <summary>
        /// <para>Square Root</para>
        /// <para>Stack transition: [f64] -> [f64]</para>
        /// <para>Pops one float, computes its square root, and pushes the result to the stack.</para>
        /// </summary>
        F64_sqrt = 0x9F,

        /// <summary>
        /// <para>Add</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, adds them, and pushes the result to the stack.</para>
        /// </summary>
        F64_add = 0xA0,

        /// <summary>
        /// <para>Sub</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, subtracts them, and pushes the result to the stack.</para>
        /// </summary>
        F64_sub = 0xA1,

        /// <summary>
        /// <para>Mul</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, multiplies them, and pushes the result to the stack.</para>
        /// </summary>
        F64_mul = 0xA2,

        /// <summary>
        /// <para>Div</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, divides them, and pushes the result to the stack.</para>
        /// </summary>
        F64_div = 0xA3,

        /// <summary>
        /// <para>Minimum</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, computes their minimum value, and pushes the result to the stack.</para>
        /// </summary>
        F64_min = 0xA4,

        /// <summary>
        /// <para>Maximum</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, computes their maximum value, and pushes the result to the stack.</para>
        /// </summary>
        F64_max = 0xA5,

        /// <summary>
        /// <para>Copysign</para>
        /// <para>Stack transition: [f64, f64] -> [f64]</para>
        /// <para>Pops two floats, copies the sign of the second to the absolute value of the first, and pushes the result.</para>
        /// </summary>
        F64_copysign = 0xA6,

        // Comparison

        // Comparison

        /// <summary>
        /// <para>I32_eqz</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops a integer, checks if it is equal to zero, and pushes the result.</para>
        /// </summary>
        I32_eqz = 0x45,

        /// <summary>
        /// <para>I32_eq</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if they are equal, and pushes the result.</para>
        /// </summary>
        I32_eq = 0x46,

        /// <summary>
        /// <para>I32_ne</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if they are not equal, and pushes the result.</para>
        /// </summary>
        I32_ne = 0x47,

        /// <summary>
        /// <para>I32_lt_s</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than the second (signed), and pushes the result.</para>
        /// </summary>
        I32_lt_s = 0x48,

        /// <summary>
        /// <para>I32_lt_u</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than the second (unsigned), and pushes the result.</para>
        /// </summary>
        I32_lt_u = 0x49,

        /// <summary>
        /// <para>I32_gt_s</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than the second (signed), and pushes the result.</para>
        /// </summary>
        I32_gt_s = 0x4A,

        /// <summary>
        /// <para>I32_gt_u</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than the second (unsigned), and pushes the result.</para>
        /// </summary>
        I32_gt_u = 0x4B,

        /// <summary>
        /// <para>I32_le_s</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than or equal to the second (signed), and pushes the result.</para>
        /// </summary>
        I32_le_s = 0x4C,

        /// <summary>
        /// <para>I32_le_u</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than or equal to the second (unsigned), and pushes the result.</para>
        /// </summary>
        I32_le_u = 0x4D,

        /// <summary>
        /// <para>I32_ge_s</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than or equal to the second (signed), and pushes the result.</para>
        /// </summary>
        I32_ge_s = 0x4E,

        /// <summary>
        /// <para>I32_ge_u</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than or equal to the second (unsigned), and pushes the result.</para>
        /// </summary>
        I32_ge_u = 0x4F,

        /// <summary>
        /// <para>I64_eqz</para>
        /// <para>Stack transition: [i64] -> [i32]</para>
        /// <para>Pops a integer, checks if it is equal to zero, and pushes the result.</para>
        /// </summary>
        I64_eqz = 0x50,

        /// <summary>
        /// <para>I64_eq</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if they are equal, and pushes the result.</para>
        /// </summary>
        I64_eq = 0x51,

        /// <summary>
        /// <para>I64_ne</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if they are not equal, and pushes the result.</para>
        /// </summary>
        I64_ne = 0x52,

        /// <summary>
        /// <para>I64_lt_s</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than the second (signed), and pushes the result.</para>
        /// </summary>
        I64_lt_s = 0x53,

        /// <summary>
        /// <para>I64_lt_u</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than the second (unsigned), and pushes the result.</para>
        /// </summary>
        I64_lt_u = 0x54,

        /// <summary>
        /// <para>I64_gt_s</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than the second (signed), and pushes the result.</para>
        /// </summary>
        I64_gt_s = 0x55,

        /// <summary>
        /// <para>I64_gt_u</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than the second (unsigned), and pushes the result.</para>
        /// </summary>
        I64_gt_u = 0x56,

        /// <summary>
        /// <para>I64_le_s</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than or equal to the second (signed), and pushes the result.</para>
        /// </summary>
        I64_le_s = 0x57,

        /// <summary>
        /// <para>I64_le_u</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is less than or equal to the second (unsigned), and pushes the result.</para>
        /// </summary>
        I64_le_u = 0x58,

        /// <summary>
        /// <para>I64_ge_s</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than or equal to the second (signed), and pushes the result.</para>
        /// </summary>
        I64_ge_s = 0x59,

        /// <summary>
        /// <para>I64_ge_u</para>
        /// <para>Stack transition: [i64, i64] -> [i32]</para>
        /// <para>Pops two integers, checks if the first is greater than or equal to the second (unsigned), and pushes the result.</para>
        /// </summary>
        I64_ge_u = 0x5A,

        /// <summary>
        /// <para>F32_eq</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if they are equal, and pushes the integer result.</para>
        /// </summary>
        F32_eq = 0x5B,

        /// <summary>
        /// <para>F32_ne</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if they are not equal, and pushes the integer result.</para>
        /// </summary>
        F32_ne = 0x5C,

        /// <summary>
        /// <para>F32_lt</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is less than the second, and pushes the integer result.</para>
        /// </summary>
        F32_lt = 0x5D,

        /// <summary>
        /// <para>F32_gt</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is greater than the second, and pushes the integer result.</para>
        /// </summary>
        F32_gt = 0x5E,

        /// <summary>
        /// <para>F32_le</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is less than or equal to the second, and pushes the integer result.</para>
        /// </summary>
        F32_le = 0x5F,

        /// <summary>
        /// <para>F32_ge</para>
        /// <para>Stack transition: [f32, f32] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is greater than or equal to the second, and pushes the integer result.</para>
        /// </summary>
        F32_ge = 0x60,

        /// <summary>
        /// <para>F64_eq</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if they are equal, and pushes the integer result.</para>
        /// </summary>
        F64_eq = 0x61,

        /// <summary>
        /// <para>F64_ne</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if they are not equal, and pushes the integer result.</para>
        /// </summary>
        F64_ne = 0x62,

        /// <summary>
        /// <para>F64_lt</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is less than the second, and pushes the integer result.</para>
        /// </summary>
        F64_lt = 0x63,

        /// <summary>
        /// <para>F64_gt</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is greater than the second, and pushes the integer result.</para>
        /// </summary>
        F64_gt = 0x64,

        /// <summary>
        /// <para>F64_le</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is less than or equal to the second, and pushes the integer result.</para>
        /// </summary>
        F64_le = 0x65,

        /// <summary>
        /// <para>F64_ge</para>
        /// <para>Stack transition: [f64, f64] -> [i32]</para>
        /// <para>Pops two floats, checks if the first is greater than or equal to the second, and pushes the integer result.</para>
        /// </summary>
        F64_ge = 0x66,

        /// <summary>
        /// <para>Call_indirect</para>
        /// <para>Stack transition: [i32...] -> [...]</para>
        /// <para>Pops a table index and calls the function stored there, checking that it has the expected type.</para>
        /// </summary>
        Call_indirect = 0x11,

        /// <summary>
        /// <para>Drop</para>
        /// <para>Stack transition: [t] -> []</para>
        /// <para>Pops a value and discards it.</para>
        /// </summary>
        Drop = 0x1A,

        /// <summary>
        /// <para>Select</para>
        /// <para>Stack transition: [t, t, i32] -> [t]</para>
        /// <para>Pops a condition and two values, and pushes the first value if the condition is non-zero, otherwise the second.</para>
        /// </summary>
        Select = 0x1B,

        /// <summary>
        /// <para>I32_and</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers and pushes their bitwise and.</para>
        /// </summary>
        I32_and = 0x71,

        /// <summary>
        /// <para>I32_or</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers and pushes their bitwise or.</para>
        /// </summary>
        I32_or = 0x72,

        /// <summary>
        /// <para>I32_xor</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops two integers and pushes their bitwise exclusive or.</para>
        /// </summary>
        I32_xor = 0x73,

        /// <summary>
        /// <para>I32_shl</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted left.</para>
        /// </summary>
        I32_shl = 0x74,

        /// <summary>
        /// <para>I32_shr_s</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted right, keeping the sign.</para>
        /// </summary>
        I32_shr_s = 0x75,

        /// <summary>
        /// <para>I32_shr_u</para>
        /// <para>Stack transition: [i32, i32] -> [i32]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted right, filling with zeros.</para>
        /// </summary>
        I32_shr_u = 0x76,

        /// <summary>
        /// <para>I64_and</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers and pushes their bitwise and.</para>
        /// </summary>
        I64_and = 0x83,

        /// <summary>
        /// <para>I64_or</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers and pushes their bitwise or.</para>
        /// </summary>
        I64_or = 0x84,

        /// <summary>
        /// <para>I64_xor</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops two integers and pushes their bitwise exclusive or.</para>
        /// </summary>
        I64_xor = 0x85,

        /// <summary>
        /// <para>I64_shl</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted left.</para>
        /// </summary>
        I64_shl = 0x86,

        /// <summary>
        /// <para>I64_shr_s</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted right, keeping the sign.</para>
        /// </summary>
        I64_shr_s = 0x87,

        /// <summary>
        /// <para>I64_shr_u</para>
        /// <para>Stack transition: [i64, i64] -> [i64]</para>
        /// <para>Pops a value and a shift count, and pushes the value shifted right, filling with zeros.</para>
        /// </summary>
        I64_shr_u = 0x88,

        /// <summary>
        /// <para>I32_wrap_i64</para>
        /// <para>Stack transition: [i64] -> [i32]</para>
        /// <para>Pops a 64-bit integer and pushes its low 32 bits.</para>
        /// </summary>
        I32_wrap_i64 = 0xA7,

        /// <summary>
        /// <para>I64_extend_i32_s</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a 32-bit integer and pushes it sign-extended to 64 bits.</para>
        /// </summary>
        I64_extend_i32_s = 0xAC,

        /// <summary>
        /// <para>I64_extend_i32_u</para>
        /// <para>Stack transition: [i32] -> [i64]</para>
        /// <para>Pops a 32-bit integer and pushes it zero-extended to 64 bits.</para>
        /// </summary>
        I64_extend_i32_u = 0xAD,

        /// <summary>
        /// <para>F32_convert_i32_s</para>
        /// <para>Stack transition: [i32] -> [f32]</para>
        /// <para>Pops a signed 32-bit integer and pushes it as a float.</para>
        /// </summary>
        F32_convert_i32_s = 0xB2,

        /// <summary>
        /// <para>F32_convert_i32_u</para>
        /// <para>Stack transition: [i32] -> [f32]</para>
        /// <para>Pops an unsigned 32-bit integer and pushes it as a float.</para>
        /// </summary>
        F32_convert_i32_u = 0xB3,

        /// <summary>
        /// <para>F32_convert_i64_s</para>
        /// <para>Stack transition: [i64] -> [f32]</para>
        /// <para>Pops a signed 64-bit integer and pushes it as a float.</para>
        /// </summary>
        F32_convert_i64_s = 0xB4,

        /// <summary>
        /// <para>F32_convert_i64_u</para>
        /// <para>Stack transition: [i64] -> [f32]</para>
        /// <para>Pops an unsigned 64-bit integer and pushes it as a float.</para>
        /// </summary>
        F32_convert_i64_u = 0xB5,

        /// <summary>
        /// <para>F32_demote_f64</para>
        /// <para>Stack transition: [f64] -> [f32]</para>
        /// <para>Pops a 64-bit float and pushes it rounded to a 32-bit float.</para>
        /// </summary>
        F32_demote_f64 = 0xB6,

        /// <summary>
        /// <para>F64_convert_i32_s</para>
        /// <para>Stack transition: [i32] -> [f64]</para>
        /// <para>Pops a signed 32-bit integer and pushes it as a double.</para>
        /// </summary>
        F64_convert_i32_s = 0xB7,

        /// <summary>
        /// <para>F64_convert_i32_u</para>
        /// <para>Stack transition: [i32] -> [f64]</para>
        /// <para>Pops an unsigned 32-bit integer and pushes it as a double.</para>
        /// </summary>
        F64_convert_i32_u = 0xB8,

        /// <summary>
        /// <para>F64_convert_i64_s</para>
        /// <para>Stack transition: [i64] -> [f64]</para>
        /// <para>Pops a signed 64-bit integer and pushes it as a double.</para>
        /// </summary>
        F64_convert_i64_s = 0xB9,

        /// <summary>
        /// <para>F64_convert_i64_u</para>
        /// <para>Stack transition: [i64] -> [f64]</para>
        /// <para>Pops an unsigned 64-bit integer and pushes it as a double.</para>
        /// </summary>
        F64_convert_i64_u = 0xBA,

        /// <summary>
        /// <para>F64_promote_f32</para>
        /// <para>Stack transition: [f32] -> [f64]</para>
        /// <para>Pops a 32-bit float and pushes it as a 64-bit float.</para>
        /// </summary>
        F64_promote_f32 = 0xBB,

        /// <summary>
        /// <para>I32_reinterpret_f32</para>
        /// <para>Stack transition: [f32] -> [i32]</para>
        /// <para>Pops a 32-bit float and pushes its bits as an integer.</para>
        /// </summary>
        I32_reinterpret_f32 = 0xBC,

        /// <summary>
        /// <para>I64_reinterpret_f64</para>
        /// <para>Stack transition: [f64] -> [i64]</para>
        /// <para>Pops a 64-bit float and pushes its bits as an integer.</para>
        /// </summary>
        I64_reinterpret_f64 = 0xBD,

        /// <summary>
        /// <para>F32_reinterpret_i32</para>
        /// <para>Stack transition: [i32] -> [f32]</para>
        /// <para>Pops a 32-bit integer and pushes its bits as a float.</para>
        /// </summary>
        F32_reinterpret_i32 = 0xBE,

        /// <summary>
        /// <para>F64_reinterpret_i64</para>
        /// <para>Stack transition: [i64] -> [f64]</para>
        /// <para>Pops a 64-bit integer and pushes its bits as a double.</para>
        /// </summary>
        F64_reinterpret_i64 = 0xBF,

        /// <summary>
        /// <para>I32_extend8_s</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops an integer and pushes its low 8 bits sign-extended.</para>
        /// </summary>
        I32_extend8_s = 0xC0,

        /// <summary>
        /// <para>I32_extend16_s</para>
        /// <para>Stack transition: [i32] -> [i32]</para>
        /// <para>Pops an integer and pushes its low 16 bits sign-extended.</para>
        /// </summary>
        I32_extend16_s = 0xC1,

        /// <summary>
        /// <para>Prefix_FC</para>
        /// <para>Stack transition: -</para>
        /// <para>Prefix for the miscellaneous instructions (saturating truncation, bulk memory). Followed by a LEB128 sub-opcode.</para>
        /// </summary>
        Prefix_FC = 0xFC,
    }
}
