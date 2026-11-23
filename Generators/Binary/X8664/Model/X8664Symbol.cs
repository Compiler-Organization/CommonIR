using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    internal class X8664Symbol
    {
        public string Name { get; set; }

        public X8664Sections Section { get; set; }

        public int Offset { get; set; } = -1;

        public bool IsBound => this.Offset >= 0;

        public X8664Symbol(string name, X8664Sections section)
        {
            this.Name = name;
            this.Section = section;
        }

        public X8664Symbol(string name, X8664Sections section, int offset)
        {
            this.Name = name;
            this.Section = section;
            this.Offset = offset;
        }

        public override string ToString() => $"{this.Name} ({this.Section}+{this.Offset})";
    }

    internal class X8664Fixup
    {
        public int Position { get; set; }

        public X8664Symbol Target { get; set; }

        public int Addend { get; set; }

        public X8664Fixup(int position, X8664Symbol target, int addend)
        {
            this.Position = position;
            this.Target = target;
            this.Addend = addend;
        }
    }

    internal readonly struct X8664Memory
    {
        public X8664Register Base { get; }

        public int Displacement { get; }

        public X8664Symbol? Symbol { get; }

        public X8664Register? Index { get; }

        public int Scale { get; }

        X8664Memory(X8664Register baseRegister, int displacement, X8664Symbol? symbol, X8664Register? index = null, int scale = 1)
        {
            this.Base = baseRegister;
            this.Displacement = displacement;
            this.Symbol = symbol;
            this.Index = index;
            this.Scale = scale;
        }

        public static X8664Memory At(X8664Register baseRegister, int displacement = 0) => new X8664Memory(baseRegister, displacement, null);

        public static X8664Memory Indexed(X8664Register baseRegister, X8664Register index, int scale, int displacement = 0) => new X8664Memory(baseRegister, displacement, null, index, scale);

        public static X8664Memory Rip(X8664Symbol symbol, int addend = 0) => new X8664Memory(X8664Register.RAX, addend, symbol);

        public X8664Memory Offset(int bytes) => new X8664Memory(this.Base, this.Displacement + bytes, this.Symbol, this.Index, this.Scale);

        public bool Uses(X8664Register register) => this.Symbol == null && (this.Base == register || this.Index == register);
    }
}
