using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal class X8664Assembler
    {
        public List<byte> Code { get; } = new List<byte>();

        public List<X8664Fixup> Fixups { get; } = new List<X8664Fixup>();

        public int Position => this.Code.Count;

        public void Bind(X8664Symbol label)
        {
            if (label.IsBound)
            {
                throw ErrorHandler.Create($"Label '{label.Name}' has already been bound.");
            }

            label.Section = X8664Sections.Text;
            label.Offset = this.Position;
        }

        public void Align(int alignment)
        {
            while (this.Position % alignment != 0)
            {
                Byte(0xCC);
            }
        }

        public void Raw(byte[] bytes)
        {
            this.Code.AddRange(bytes);
        }

        public void Patch32(int position, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            for (int i = 0; i < 4; i++)
            {
                this.Code[position + i] = bytes[i];
            }
        }

        public void PatchBytes(int position, byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                this.Code[position + i] = bytes[i];
            }
        }

        #region Encoding

        void Byte(int value) => this.Code.Add((byte)value);

        void Int32(int value) => this.Code.AddRange(BitConverter.GetBytes(value));

        void Int64(long value) => this.Code.AddRange(BitConverter.GetBytes(value));

        static bool IsByteRegisterNeedingRex(int register) => register is >= 4 and <= 7;

        void Rex(bool wide, int reg, int rm, bool force)
        {
            int rex = 0x40 | (wide ? 0x08 : 0) | (((reg >> 3) & 1) << 2) | ((rm >> 3) & 1);

            if (rex != 0x40 || force)
            {
                Byte(rex);
            }
        }

        void ModRMRegister(int reg, int rm) => Byte(0xC0 | ((reg & 7) << 3) | (rm & 7));

        void ModRMMemory(int reg, X8664Memory memory)
        {
            if (memory.Symbol != null)
            {
                Byte(((reg & 7) << 3) | 0x05);
                this.Fixups.Add(new X8664Fixup(this.Position, memory.Symbol, memory.Displacement));
                Int32(0);
                return;
            }

            int baseRegister = (int)memory.Base;

            Byte(0x80 | ((reg & 7) << 3) | (baseRegister & 7));

            if ((baseRegister & 7) == 4)
            {
                Byte(0x24);
            }

            Int32(memory.Displacement);
        }

        void EncodeRegister(int prefix, bool wide, byte[] opcode, int reg, int rm, bool forceRex = false)
        {
            if (prefix != 0)
            {
                Byte(prefix);
            }

            Rex(wide, reg, rm, forceRex);

            foreach (byte b in opcode)
            {
                Byte(b);
            }

            ModRMRegister(reg, rm);
        }

        void EncodeMemory(int prefix, bool wide, byte[] opcode, int reg, X8664Memory memory, bool forceRex = false)
        {
            if (prefix != 0)
            {
                Byte(prefix);
            }

            Rex(wide, reg, memory.Symbol != null ? 0 : (int)memory.Base, forceRex);

            foreach (byte b in opcode)
            {
                Byte(b);
            }

            ModRMMemory(reg, memory);
        }

        void RelativeTarget(X8664Symbol target)
        {
            this.Fixups.Add(new X8664Fixup(this.Position, target, 0));
            Int32(0);
        }

        #endregion

        #region General purpose

        public void Mov(X8664Register destination, X8664Register source)
        {
            if (destination != source)
            {
                EncodeRegister(0, true, [0x89], (int)source, (int)destination);
            }
        }

        public void Mov32(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, false, [0x89], (int)source, (int)destination);
        }

        public void MovImmediate(X8664Register destination, long value)
        {
            int register = (int)destination;

            if (value >= 0 && value <= uint.MaxValue)
            {
                Rex(false, 0, register, false);
                Byte(0xB8 + (register & 7));
                Int32(unchecked((int)(uint)value));
            }
            else if (value >= int.MinValue && value <= int.MaxValue)
            {
                EncodeRegister(0, true, [0xC7], 0, register);
                Int32((int)value);
            }
            else
            {
                Rex(true, 0, register, false);
                Byte(0xB8 + (register & 7));
                Int64(value);
            }
        }

        public void Load(int size, bool signed, X8664Register destination, X8664Memory memory)
        {
            int register = (int)destination;

            switch (size)
            {
                case 1:
                    if (signed)
                    {
                        EncodeMemory(0, true, [0x0F, 0xBE], register, memory);
                    }
                    else
                    {
                        EncodeMemory(0, false, [0x0F, 0xB6], register, memory);
                    }
                    break;

                case 2:
                    if (signed)
                    {
                        EncodeMemory(0, true, [0x0F, 0xBF], register, memory);
                    }
                    else
                    {
                        EncodeMemory(0, false, [0x0F, 0xB7], register, memory);
                    }
                    break;

                case 4:
                    if (signed)
                    {
                        EncodeMemory(0, true, [0x63], register, memory);
                    }
                    else
                    {
                        EncodeMemory(0, false, [0x8B], register, memory);
                    }
                    break;

                case 8:
                    EncodeMemory(0, true, [0x8B], register, memory);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot load a value of {size} bytes.");
            }
        }

        public void Load(X8664Register destination, X8664Memory memory) => Load(8, false, destination, memory);

        public void Store(int size, X8664Memory memory, X8664Register source)
        {
            int register = (int)source;

            switch (size)
            {
                case 1:
                    EncodeMemory(0, false, [0x88], register, memory, IsByteRegisterNeedingRex(register));
                    break;

                case 2:
                    EncodeMemory(0x66, false, [0x89], register, memory);
                    break;

                case 4:
                    EncodeMemory(0, false, [0x89], register, memory);
                    break;

                case 8:
                    EncodeMemory(0, true, [0x89], register, memory);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot store a value of {size} bytes.");
            }
        }

        public void Store(X8664Memory memory, X8664Register source) => Store(8, memory, source);

        public void Lea(X8664Register destination, X8664Memory memory)
        {
            EncodeMemory(0, true, [0x8D], (int)destination, memory);
        }

        static byte AluOpcode(X8664AluOperation operation) => operation switch
        {
            X8664AluOperation.Add => 0x01,
            X8664AluOperation.Or => 0x09,
            X8664AluOperation.And => 0x21,
            X8664AluOperation.Subtract => 0x29,
            X8664AluOperation.Xor => 0x31,
            X8664AluOperation.Compare => 0x39,
            _ => throw ErrorHandler.Create($"Unknown ALU operation '{operation}'.")
        };

        static int AluExtension(X8664AluOperation operation) => operation switch
        {
            X8664AluOperation.Add => 0,
            X8664AluOperation.Or => 1,
            X8664AluOperation.And => 4,
            X8664AluOperation.Subtract => 5,
            X8664AluOperation.Xor => 6,
            X8664AluOperation.Compare => 7,
            _ => throw ErrorHandler.Create($"Unknown ALU operation '{operation}'.")
        };

        public void Alu(X8664AluOperation operation, X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, true, [AluOpcode(operation)], (int)source, (int)destination);
        }

        public void Alu(X8664AluOperation operation, X8664Register destination, X8664Memory source)
        {
            EncodeMemory(0, true, [(byte)(AluOpcode(operation) + 2)], (int)destination, source);
        }

        public void AluImmediate(X8664AluOperation operation, X8664Register destination, int value)
        {
            if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
            {
                EncodeRegister(0, true, [0x83], AluExtension(operation), (int)destination);
                Byte(value);
            }
            else
            {
                EncodeRegister(0, true, [0x81], AluExtension(operation), (int)destination);
                Int32(value);
            }
        }

        public void Imul(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, true, [0x0F, 0xAF], (int)destination, (int)source);
        }

        public void ImulImmediate(X8664Register destination, X8664Register source, int value)
        {
            EncodeRegister(0, true, [0x69], (int)destination, (int)source);
            Int32(value);
        }

        public void Cqo()
        {
            Byte(0x48);
            Byte(0x99);
        }

        public void Divide(bool signed, X8664Register divisor)
        {
            EncodeRegister(0, true, [0xF7], signed ? 7 : 6, (int)divisor);
        }

        public void Negate(X8664Register register)
        {
            EncodeRegister(0, true, [0xF7], 3, (int)register);
        }

        public void Not(X8664Register register)
        {
            EncodeRegister(0, true, [0xF7], 2, (int)register);
        }

        public void Shift(X8664ShiftOperation operation, X8664Register register)
        {
            EncodeRegister(0, true, [0xD3], (int)operation, (int)register);
        }

        public void ShiftImmediate(X8664ShiftOperation operation, X8664Register register, int count)
        {
            EncodeRegister(0, true, [0xC1], (int)operation, (int)register);
            Byte(count);
        }

        public void Test(X8664Register left, X8664Register right)
        {
            EncodeRegister(0, true, [0x85], (int)right, (int)left);
        }

        public void Test(X8664Memory memory, X8664Register register)
        {
            EncodeMemory(0, false, [0x85], (int)register, memory);
        }

        public void SetCondition(X8664Condition condition, X8664Register destination)
        {
            int register = (int)destination;
            EncodeRegister(0, false, [0x0F, (byte)(0x90 + (int)condition)], 0, register, IsByteRegisterNeedingRex(register));
        }

        public void MovZeroExtend8(X8664Register destination, X8664Register source)
        {
            int register = (int)source;
            EncodeRegister(0, false, [0x0F, 0xB6], (int)destination, register, IsByteRegisterNeedingRex(register));
        }

        public void MovZeroExtend16(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, false, [0x0F, 0xB7], (int)destination, (int)source);
        }

        public void MovSignExtend8(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, true, [0x0F, 0xBE], (int)destination, (int)source);
        }

        public void MovSignExtend16(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, true, [0x0F, 0xBF], (int)destination, (int)source);
        }

        public void MovSignExtend32(X8664Register destination, X8664Register source)
        {
            EncodeRegister(0, true, [0x63], (int)destination, (int)source);
        }

        public void Push(X8664Register register)
        {
            Rex(false, 0, (int)register, false);
            Byte(0x50 + ((int)register & 7));
        }

        public void Pop(X8664Register register)
        {
            Rex(false, 0, (int)register, false);
            Byte(0x58 + ((int)register & 7));
        }

        #endregion

        #region Control flow

        public void Jump(X8664Symbol target)
        {
            Byte(0xE9);
            RelativeTarget(target);
        }

        public void JumpIf(X8664Condition condition, X8664Symbol target)
        {
            Byte(0x0F);
            Byte(0x80 + (int)condition);
            RelativeTarget(target);
        }

        public void Call(X8664Symbol target)
        {
            Byte(0xE8);
            RelativeTarget(target);
        }

        public void Call(X8664Memory memory)
        {
            EncodeMemory(0, false, [0xFF], 2, memory);
        }

        public void Call(X8664Register register)
        {
            EncodeRegister(0, false, [0xFF], 2, (int)register);
        }

        public void Return()
        {
            Byte(0xC3);
        }

        public void Nop(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Byte(0x90);
            }
        }

        #endregion

        #region SSE

        static int ScalarPrefix(bool isDouble) => isDouble ? 0xF2 : 0xF3;

        public void LoadScalar(bool isDouble, X8664XmmRegister destination, X8664Memory memory)
        {
            EncodeMemory(ScalarPrefix(isDouble), false, [0x0F, 0x10], (int)destination, memory);
        }

        public void StoreScalar(bool isDouble, X8664Memory memory, X8664XmmRegister source)
        {
            EncodeMemory(ScalarPrefix(isDouble), false, [0x0F, 0x11], (int)source, memory);
        }

        public void MovXmm(X8664XmmRegister destination, X8664XmmRegister source)
        {
            if (destination != source)
            {
                EncodeRegister(0, false, [0x0F, 0x28], (int)destination, (int)source);
            }
        }

        public void ScalarArithmetic(X8664SseOperation operation, bool isDouble, X8664XmmRegister destination, X8664XmmRegister source)
        {
            EncodeRegister(ScalarPrefix(isDouble), false, [0x0F, (byte)operation], (int)destination, (int)source);
        }

        public void CompareScalar(bool isDouble, X8664XmmRegister left, X8664XmmRegister right)
        {
            EncodeRegister(isDouble ? 0x66 : 0, false, [0x0F, 0x2E], (int)left, (int)right);
        }

        public void ConvertIntegerToScalar(bool isDouble, X8664XmmRegister destination, X8664Register source)
        {
            EncodeRegister(ScalarPrefix(isDouble), true, [0x0F, 0x2A], (int)destination, (int)source);
        }

        public void ConvertScalarToInteger(bool isDouble, X8664Register destination, X8664XmmRegister source)
        {
            EncodeRegister(ScalarPrefix(isDouble), true, [0x0F, 0x2C], (int)destination, (int)source);
        }

        public void ConvertSingleToDouble(X8664XmmRegister destination, X8664XmmRegister source)
        {
            EncodeRegister(0xF3, false, [0x0F, 0x5A], (int)destination, (int)source);
        }

        public void ConvertDoubleToSingle(X8664XmmRegister destination, X8664XmmRegister source)
        {
            EncodeRegister(0xF2, false, [0x0F, 0x5A], (int)destination, (int)source);
        }

        public void MovToXmm(X8664XmmRegister destination, X8664Register source)
        {
            EncodeRegister(0x66, true, [0x0F, 0x6E], (int)destination, (int)source);
        }

        public void MovFromXmm(X8664Register destination, X8664XmmRegister source)
        {
            EncodeRegister(0x66, true, [0x0F, 0x7E], (int)source, (int)destination);
        }

        public void Xorps(X8664XmmRegister destination, X8664XmmRegister source)
        {
            EncodeRegister(0, false, [0x0F, 0x57], (int)destination, (int)source);
        }

        #endregion
    }
}
