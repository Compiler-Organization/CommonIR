using CommonIR.Errors;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.Xtensa.Emission
{
    internal enum XtensaRegister
    {
        A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15,
    }

    internal enum XtensaBranch
    {
        Equal,
        NotEqual,
        Less,
        GreaterOrEqual,
        LessUnsigned,
        GreaterOrEqualUnsigned,
    }

    internal enum XtensaSpecialRegister
    {
        SAR = 3,
        LITBASE = 5,
        EPC1 = 177,
        DEPC = 192,
        EXCSAVE1 = 209,
        CPENABLE = 224,
        PS = 230,
        VECBASE = 231,
        EXCCAUSE = 232,
        CCOUNT = 234,
        EXCVADDR = 238,
    }

    internal sealed class XtensaLabel
    {
        public string Name { get; }

        public int Position { get; set; } = -1;

        public bool IsBound => this.Position >= 0;

        public XtensaLabel(string name)
        {
            this.Name = name;
        }

        public override string ToString() => this.Name;
    }

    internal sealed class XtensaSymbol
    {
        public string Name { get; }

        public XtensaSection Section { get; set; }

        public int Offset { get; set; } = -1;

        public bool IsBound => this.Offset >= 0;

        public XtensaSymbol(string name, XtensaSection section, int offset = -1)
        {
            this.Name = name;
            this.Section = section;
            this.Offset = offset;
        }

        public override string ToString() => this.Name;
    }

    internal enum XtensaSection
    {
        Code,
        Data,
    }

    internal readonly record struct XtensaLiteral(long Value, XtensaSymbol? Symbol);

    internal enum XtensaFixupKind
    {
        Branch8,
        Branch12,
        Jump,
        Call,
    }

    internal sealed class XtensaAssembler
    {
        public List<byte> Code { get; } = new List<byte>();

        public List<(int Position, XtensaFixupKind Kind, XtensaLabel Target)> LocalFixups { get; } = new();

        public List<(int Position, XtensaSymbol Target)> CallFixups { get; } = new();

        public List<XtensaLiteral> Literals { get; } = new List<XtensaLiteral>();

        public List<(int Position, int Literal)> LiteralLoads { get; } = new();

        Dictionary<XtensaLiteral, int> LiteralIndices { get; } = new Dictionary<XtensaLiteral, int>();

        public int Position => this.Code.Count;

        public static int Index(XtensaRegister register) => (int)register;

        void Emit24(int word)
        {
            this.Code.Add((byte)word);
            this.Code.Add((byte)(word >> 8));
            this.Code.Add((byte)(word >> 16));
        }

        void Rrr(int op2, int op1, int r, int s, int t, int op0 = 0)
        {
            Emit24((op2 & 15) << 20 | (op1 & 15) << 16 | (r & 15) << 12 | (s & 15) << 8 | (t & 15) << 4 | (op0 & 15));
        }

        static int R(XtensaRegister register) => (int)register;

        public void Bind(XtensaLabel label)
        {
            if (label.IsBound)
            {
                throw ErrorHandler.Create($"Label '{label.Name}' was bound twice.");
            }

            label.Position = this.Position;
        }

        public void Align(int alignment)
        {
            while (this.Position % alignment != 0)
            {
                this.Code.Add(0);
            }
        }

        public void Raw(byte[] bytes) => this.Code.AddRange(bytes);

        public void Word(uint value)
        {
            this.Code.Add((byte)value);
            this.Code.Add((byte)(value >> 8));
            this.Code.Add((byte)(value >> 16));
            this.Code.Add((byte)(value >> 24));
        }

        public void Add(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(8, 0, R(r), R(s), R(t));

        public void AddShifted(int shift, XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(shift switch { 1 => 9, 2 => 10, 3 => 11, _ => throw ErrorHandler.Create("ADDX shift must be 1, 2 or 3.") }, 0, R(r), R(s), R(t));

        public void Sub(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(12, 0, R(r), R(s), R(t));

        public void And(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(1, 0, R(r), R(s), R(t));

        public void Or(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(2, 0, R(r), R(s), R(t));

        public void Xor(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(3, 0, R(r), R(s), R(t));

        public void Mov(XtensaRegister r, XtensaRegister s)
        {
            if (r != s)
            {
                Or(r, s, s);
            }
        }

        public void Neg(XtensaRegister r, XtensaRegister t) => Rrr(6, 0, R(r), 0, R(t));

        public void Mull(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(8, 2, R(r), R(s), R(t));

        public void Muluh(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(10, 2, R(r), R(s), R(t));

        public void Mulsh(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(11, 2, R(r), R(s), R(t));

        public void Quou(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(12, 2, R(r), R(s), R(t));

        public void Quos(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(13, 2, R(r), R(s), R(t));

        public void Remu(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(14, 2, R(r), R(s), R(t));

        public void Rems(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(15, 2, R(r), R(s), R(t));

        public void Min(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(4, 3, R(r), R(s), R(t));

        public void Max(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(5, 3, R(r), R(s), R(t));

        public void Minu(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(6, 3, R(r), R(s), R(t));

        public void Maxu(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(7, 3, R(r), R(s), R(t));

        public void Moveqz(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(8, 3, R(r), R(s), R(t));

        public void Movnez(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(9, 3, R(r), R(s), R(t));

        public void Movltz(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(10, 3, R(r), R(s), R(t));

        public void Movgez(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(11, 3, R(r), R(s), R(t));

        public void Sext(XtensaRegister r, XtensaRegister s, int bit)
        {
            if (bit < 7 || bit > 22)
            {
                throw ErrorHandler.Create("SEXT bit position must be between 7 and 22.");
            }

            Rrr(2, 3, R(r), R(s), bit - 7);
        }

        public void Nsau(XtensaRegister t, XtensaRegister s) => Rrr(4, 0, 15, R(s), R(t));

        public void Rsr(XtensaRegister t, XtensaSpecialRegister register) => Rrr(0, 3, (int)register >> 4, (int)register, R(t));

        public void Wsr(XtensaRegister t, XtensaSpecialRegister register) => Rrr(1, 3, (int)register >> 4, (int)register, R(t));

        public void Rsil(XtensaRegister t, int level) => Rrr(0, 0, 6, level, R(t));

        public void Waiti(int level) => Rrr(0, 0, 7, level, 0);

        public void Ssl(XtensaRegister s) => Rrr(4, 0, 1, R(s), 0);

        public void Ssr(XtensaRegister s) => Rrr(4, 0, 0, R(s), 0);

        public void Ssai(int amount) => Rrr(4, 0, 4, amount & 15, (amount >> 4) & 1);

        public void Sll(XtensaRegister r, XtensaRegister s) => Rrr(10, 1, R(r), R(s), 0);

        public void Srl(XtensaRegister r, XtensaRegister t) => Rrr(9, 1, R(r), 0, R(t));

        public void Sra(XtensaRegister r, XtensaRegister t) => Rrr(11, 1, R(r), 0, R(t));

        public void Src(XtensaRegister r, XtensaRegister s, XtensaRegister t) => Rrr(8, 1, R(r), R(s), R(t));

        public void Slli(XtensaRegister r, XtensaRegister s, int amount)
        {
            if (amount == 0)
            {
                Mov(r, s);
                return;
            }

            if (amount < 1 || amount > 31)
            {
                throw ErrorHandler.Create($"SLLI amount {amount} is out of range.");
            }

            int encoded = 32 - amount;
            Rrr((encoded >> 4) & 1, 1, R(r), R(s), encoded & 15);
        }

        public void Srai(XtensaRegister r, XtensaRegister t, int amount)
        {
            if (amount < 0 || amount > 31)
            {
                throw ErrorHandler.Create($"SRAI amount {amount} is out of range.");
            }

            Rrr(2 | ((amount >> 4) & 1), 1, R(r), amount & 15, R(t));
        }

        public void Srli(XtensaRegister r, XtensaRegister t, int amount)
        {
            if (amount < 0 || amount > 31)
            {
                throw ErrorHandler.Create($"SRLI amount {amount} is out of range.");
            }

            if (amount > 15)
            {
                Extui(r, t, amount, 32 - amount);
                return;
            }

            Rrr(4, 1, R(r), amount, R(t));
        }

        public void Extui(XtensaRegister r, XtensaRegister t, int shift, int bits)
        {
            if (bits < 1 || bits > 16 || shift < 0 || shift > 31)
            {
                throw ErrorHandler.Create($"EXTUI {shift}, {bits} is out of range.");
            }

            Rrr(bits - 1, 4 | ((shift >> 4) & 1), R(r), shift & 15, R(t));
        }

        void Lsai(int r, XtensaRegister t, XtensaRegister s, int immediate) => Emit24((immediate & 0xFF) << 16 | r << 12 | R(s) << 8 | R(t) << 4 | 2);

        public static bool FitsLoad(int size, int offset) => offset >= 0 && offset % size == 0 && offset / size <= 255;

        public void Load(int size, bool signed, XtensaRegister t, XtensaRegister s, int offset)
        {
            if (!FitsLoad(size, offset))
            {
                throw ErrorHandler.Create($"Load offset {offset} does not fit.");
            }

            switch (size)
            {
                case 1:
                    Lsai(0, t, s, offset);

                    if (signed)
                    {
                        Sext(t, t, 7);
                    }
                    break;

                case 2:
                    Lsai(signed ? 9 : 1, t, s, offset / 2);
                    break;

                case 4:
                    Lsai(2, t, s, offset / 4);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot load {size} bytes.");
            }
        }

        public void Store(int size, XtensaRegister t, XtensaRegister s, int offset)
        {
            if (!FitsLoad(size, offset))
            {
                throw ErrorHandler.Create($"Store offset {offset} does not fit.");
            }

            switch (size)
            {
                case 1:
                    Lsai(4, t, s, offset);
                    break;

                case 2:
                    Lsai(5, t, s, offset / 2);
                    break;

                case 4:
                    Lsai(6, t, s, offset / 4);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot store {size} bytes.");
            }
        }

        public void L32i(XtensaRegister t, XtensaRegister s, int offset) => Load(4, false, t, s, offset);

        public void S32i(XtensaRegister t, XtensaRegister s, int offset) => Store(4, t, s, offset);

        public void Addi(XtensaRegister t, XtensaRegister s, int immediate)
        {
            if (immediate < -128 || immediate > 127)
            {
                throw ErrorHandler.Create($"ADDI immediate {immediate} is out of range.");
            }

            Lsai(12, t, s, immediate);
        }

        public void Addmi(XtensaRegister t, XtensaRegister s, int immediate)
        {
            if (immediate % 256 != 0 || immediate < -32768 || immediate > 32512)
            {
                throw ErrorHandler.Create($"ADDMI immediate {immediate} is out of range.");
            }

            Lsai(13, t, s, immediate >> 8);
        }

        public static bool FitsMovi(long value) => value >= -2048 && value <= 2047;

        public void Movi(XtensaRegister t, int immediate)
        {
            if (!FitsMovi(immediate))
            {
                throw ErrorHandler.Create($"MOVI immediate {immediate} is out of range.");
            }

            Emit24((immediate & 0xFF) << 16 | 10 << 12 | ((immediate >> 8) & 15) << 8 | R(t) << 4 | 2);
        }

        public void AddConstant(XtensaRegister t, XtensaRegister s, int value, XtensaRegister scratch)
        {
            if (value == 0)
            {
                Mov(t, s);
                return;
            }

            if (value >= -128 && value <= 127)
            {
                Addi(t, s, value);
                return;
            }

            int high = (value + 128) & ~255;
            int low = value - high;

            if (high >= -32768 && high <= 32512 && low >= -128 && low <= 127)
            {
                Addmi(t, s, high);

                if (low != 0)
                {
                    Addi(t, t, low);
                }

                return;
            }

            LoadConstant(scratch, value);
            Add(t, s, scratch);
        }

        public void LoadConstant(XtensaRegister t, long value)
        {
            int word = unchecked((int)value);

            if (FitsMovi(word))
            {
                Movi(t, word);
                return;
            }

            LoadLiteral(t, new XtensaLiteral((uint)word, null));
        }

        public void LoadAddress(XtensaRegister t, XtensaSymbol symbol, int addend = 0) => LoadLiteral(t, new XtensaLiteral(addend, symbol));

        public void LoadLiteral(XtensaRegister t, XtensaLiteral literal)
        {
            if (!this.LiteralIndices.TryGetValue(literal, out int index))
            {
                index = this.Literals.Count;
                this.Literals.Add(literal);
                this.LiteralIndices[literal] = index;
            }

            this.LiteralLoads.Add((this.Position, index));
            Emit24(R(t) << 4 | 1);
        }

        public void Branch(XtensaBranch condition, XtensaRegister s, XtensaRegister t, XtensaLabel target)
        {
            int r = condition switch
            {
                XtensaBranch.Equal => 1,
                XtensaBranch.Less => 2,
                XtensaBranch.LessUnsigned => 3,
                XtensaBranch.NotEqual => 9,
                XtensaBranch.GreaterOrEqual => 10,
                XtensaBranch.GreaterOrEqualUnsigned => 11,
                _ => throw ErrorHandler.Create($"Unknown branch {condition}."),
            };

            ShortOrLong(target, 8, (skip) => Emit24(r << 12 | R(s) << 8 | R(t) << 4 | 7), () => Branch(Invert(condition), s, t, null!));
        }

        public static XtensaBranch Invert(XtensaBranch condition) => condition switch
        {
            XtensaBranch.Equal => XtensaBranch.NotEqual,
            XtensaBranch.NotEqual => XtensaBranch.Equal,
            XtensaBranch.Less => XtensaBranch.GreaterOrEqual,
            XtensaBranch.GreaterOrEqual => XtensaBranch.Less,
            XtensaBranch.LessUnsigned => XtensaBranch.GreaterOrEqualUnsigned,
            XtensaBranch.GreaterOrEqualUnsigned => XtensaBranch.LessUnsigned,
            _ => throw ErrorHandler.Create($"Unknown branch {condition}."),
        };

        public void BranchZero(bool zero, XtensaRegister s, XtensaLabel target) => BranchZeroCondition(zero ? 0 : 1, s, target);

        public void BranchNegative(bool negative, XtensaRegister s, XtensaLabel target) => BranchZeroCondition(negative ? 2 : 3, s, target);

        void BranchZeroCondition(int m, XtensaRegister s, XtensaLabel target)
        {
            ShortOrLong(target, 12, (_) => Emit24(R(s) << 8 | m << 6 | 1 << 4 | 6), () => BranchZeroCondition(m ^ 1, s, null!));
        }

        public void BranchBit(bool set, XtensaRegister s, int bit, XtensaLabel target)
        {
            ShortOrLong(target, 8, (_) => Emit24((set ? 14 : 6) + ((bit >> 4) & 1) << 12 | R(s) << 8 | (bit & 15) << 4 | 7), () => BranchBit(!set, s, bit, null!));
        }

        public HashSet<int> LongBranches { get; set; } = new HashSet<int>();

        public List<int> FailedBranches { get; } = new List<int>();

        int BranchCount { get; set; }

        List<(int Position, int Bits, XtensaLabel Target, int Index)> ShortFixups { get; } = new();

        void ShortOrLong(XtensaLabel? target, int bits, Action<bool> emitShort, Action emitInvertedOver)
        {
            if (target == null)
            {
                int position = this.Position;
                emitShort(true);
                SetBranchOffset(position, bits, 2);
                return;
            }

            int index = this.BranchCount++;
            int limit = 1 << (bits - 1);

            if (target.IsBound)
            {
                int distance = target.Position - (this.Position + 4);

                if (distance >= -limit && distance < limit)
                {
                    int position = this.Position;
                    emitShort(false);
                    SetBranchOffset(position, bits, distance);
                    return;
                }
            }
            else if (!this.LongBranches.Contains(index))
            {
                this.ShortFixups.Add((this.Position, bits, target, index));
                emitShort(false);
                return;
            }

            emitInvertedOver();
            Jump(target);
        }

        public void BranchBoolean(bool whenTrue, int booleanRegister, XtensaLabel target)
        {
            ShortOrLong(target, 8, (_) => Emit24((whenTrue ? 1 : 0) << 12 | booleanRegister << 8 | 0x76), () => BranchBoolean(!whenTrue, booleanRegister, null!));
        }

        public void Wfr(int f, XtensaRegister s) => Rrr(15, 10, f, R(s), 5);

        public void Rfr(XtensaRegister r, int f) => Rrr(15, 10, R(r), f, 4);

        public void FloatArithmetic(int op2, int r, int s, int t) => Rrr(op2, 10, r, s, t);

        public void FloatCompare(int op2, int b, int s, int t) => Rrr(op2, 11, b, s, t);

        public void FloatFromInteger(bool unsigned, int f, XtensaRegister s) => Rrr(unsigned ? 13 : 12, 10, f, R(s), 0);

        public void IntegerFromFloat(bool unsigned, XtensaRegister r, int f) => Rrr(unsigned ? 14 : 9, 10, R(r), f, 0);

        public void Movt(bool onTrue, XtensaRegister r, XtensaRegister s, int b) => Rrr(onTrue ? 13 : 12, 3, R(r), R(s), b);

        public void Raw24(int word) => Emit24(word);

        void SetBranchOffset(int position, int bits, int offset)
        {
            int word = this.Code[position] | this.Code[position + 1] << 8 | this.Code[position + 2] << 16;

            if (bits == 8)
            {
                word = (word & 0x00FFFF) | (offset & 0xFF) << 16;
            }
            else
            {
                word = (word & 0x000FFF) | (offset & 0xFFF) << 12;
            }

            this.Code[position] = (byte)word;
            this.Code[position + 1] = (byte)(word >> 8);
            this.Code[position + 2] = (byte)(word >> 16);
        }

        public void Jump(XtensaLabel target)
        {
            this.LocalFixups.Add((this.Position, XtensaFixupKind.Jump, target));
            Emit24(6);
        }

        public void Jx(XtensaRegister s) => Rrr(0, 0, 0, R(s), 10);

        public void Callx0(XtensaRegister s) => Rrr(0, 0, 0, R(s), 12);

        public void Ret() => Emit24(0x80);

        public void Nop() => Emit24(0x20F0);

        public void Memw() => Emit24(0x20C0);

        public void Isync() => Emit24(0x2000);

        public void Rsync() => Emit24(0x2010);

        public void Dsync() => Emit24(0x2030);

        public void Ill() => Emit24(0);

        public void Call0(XtensaSymbol target)
        {
            AlignCall();
            this.CallFixups.Add((this.Position, target));
            Emit24(5);
        }

        void NarrowNop()
        {
            this.Code.Add(0x3D);
            this.Code.Add(0xF0);
        }

        void AlignCall()
        {
            switch (this.Position % 4)
            {
                case 0:
                    NarrowNop();
                    Nop();
                    break;

                case 2:
                    Nop();
                    break;

                case 3:
                    NarrowNop();
                    break;
            }
        }

        public void Callx0Aligned(XtensaRegister s)
        {
            AlignCall();
            Callx0(s);
        }

        public void ResolveLocal()
        {
            foreach ((int position, int bits, XtensaLabel target, int index) in this.ShortFixups)
            {
                if (!target.IsBound)
                {
                    throw ErrorHandler.Create($"Label '{target.Name}' was never bound.");
                }

                int distance = target.Position - (position + 4);
                int limit = 1 << (bits - 1);

                if (distance < -limit || distance >= limit)
                {
                    this.FailedBranches.Add(index);
                    continue;
                }

                SetBranchOffset(position, bits, distance);
            }

            this.ShortFixups.Clear();

            foreach ((int position, XtensaFixupKind kind, XtensaLabel target) in this.LocalFixups)
            {
                if (!target.IsBound)
                {
                    throw ErrorHandler.Create($"Label '{target.Name}' was never bound.");
                }

                int offset = target.Position - (position + 4);

                switch (kind)
                {
                    case XtensaFixupKind.Jump:
                        if (offset < -(1 << 17) || offset >= (1 << 17))
                        {
                            throw ErrorHandler.Create($"Jump to '{target.Name}' is out of range.");
                        }

                        int word = (offset & 0x3FFFF) << 6 | 6;
                        this.Code[position] = (byte)word;
                        this.Code[position + 1] = (byte)(word >> 8);
                        this.Code[position + 2] = (byte)(word >> 16);
                        break;

                    default:
                        throw ErrorHandler.Create($"Unexpected local fixup {kind}.");
                }
            }

            this.LocalFixups.Clear();
        }

        public void Patch24(int position, int word)
        {
            this.Code[position] = (byte)word;
            this.Code[position + 1] = (byte)(word >> 8);
            this.Code[position + 2] = (byte)(word >> 16);
        }

        public int Read24(int position) => this.Code[position] | this.Code[position + 1] << 8 | this.Code[position + 2] << 16;
    }
}
