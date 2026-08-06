namespace CommonIR.Generators.WASM.Model
{
    internal enum WasmInstructions : byte
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
    }
}
