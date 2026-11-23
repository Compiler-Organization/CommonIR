using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    public enum X8664Register
    {
        RAX = 0,
        RCX = 1,
        RDX = 2,
        RBX = 3,
        RSP = 4,
        RBP = 5,
        RSI = 6,
        RDI = 7,
        R8 = 8,
        R9 = 9,
        R10 = 10,
        R11 = 11,
        R12 = 12,
        R13 = 13,
        R14 = 14,
        R15 = 15,
    }

    public enum X8664XmmRegister
    {
        XMM0 = 0,
        XMM1 = 1,
        XMM2 = 2,
        XMM3 = 3,
        XMM4 = 4,
        XMM5 = 5,
        XMM6 = 6,
        XMM7 = 7,
        XMM8 = 8,
        XMM9 = 9,
        XMM10 = 10,
        XMM11 = 11,
        XMM12 = 12,
        XMM13 = 13,
        XMM14 = 14,
        XMM15 = 15,
    }

    public enum X8664Condition
    {
        Overflow = 0x0,
        NoOverflow = 0x1,
        Below = 0x2,
        AboveOrEqual = 0x3,
        Equal = 0x4,
        NotEqual = 0x5,
        BelowOrEqual = 0x6,
        Above = 0x7,
        Sign = 0x8,
        NoSign = 0x9,
        Parity = 0xA,
        NoParity = 0xB,
        Less = 0xC,
        GreaterOrEqual = 0xD,
        LessOrEqual = 0xE,
        Greater = 0xF,
    }

    public enum X8664AluOperation
    {
        Add,
        Or,
        And,
        Subtract,
        Xor,
        Compare,
    }

    public enum X8664ShiftOperation
    {
        ShiftLeft = 4,
        ShiftRightLogical = 5,
        ShiftRightArithmetic = 7,
    }

    public enum X8664SseOperation
    {
        Add = 0x58,
        Multiply = 0x59,
        Subtract = 0x5C,
        Divide = 0x5E,
        SquareRoot = 0x51,
    }

    public enum X8664Sections
    {
        Text,
        ReadOnlyData,
        Data,
    }
}
