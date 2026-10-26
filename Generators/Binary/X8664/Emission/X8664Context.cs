using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Model;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal class X8664Context
    {
        public X8664Assembler Assembler { get; } = new X8664Assembler();

        public Dictionary<IRFunction, X8664Symbol> Functions { get; } = new Dictionary<IRFunction, X8664Symbol>();

        public Dictionary<IRFunctionImport, X8664Symbol> Imports { get; } = new Dictionary<IRFunctionImport, X8664Symbol>();

        public Dictionary<IRGlobal, X8664Symbol> Globals { get; } = new Dictionary<IRGlobal, X8664Symbol>();

        public Dictionary<IRValueInstruction, X8664Symbol> Constants { get; } = new Dictionary<IRValueInstruction, X8664Symbol>();

        public List<byte> ReadOnlyData { get; } = new List<byte>();

        Dictionary<string, X8664Symbol> Messages { get; } = new Dictionary<string, X8664Symbol>(StringComparer.Ordinal);

        public X8664Symbol Probe { get; } = new X8664Symbol("__probe", X8664Sections.Text);

        public IRFunction Malloc { get; set; } = null!;

        public IRFunction Panic { get; set; } = null!;

        public IRFunction Await { get; set; } = null!;

        public Dictionary<IRFunction, IRFunction> Dispatchers { get; } = new Dictionary<IRFunction, IRFunction>();

        public List<X8664UnwindEntry> UnwindEntries { get; } = new List<X8664UnwindEntry>();

        public X8664Symbol AddReadOnly(string name, byte[] bytes, int alignment)
        {
            while (this.ReadOnlyData.Count % alignment != 0)
            {
                this.ReadOnlyData.Add(0);
            }

            X8664Symbol symbol = new X8664Symbol(name, X8664Sections.ReadOnlyData, this.ReadOnlyData.Count);
            this.ReadOnlyData.AddRange(bytes);

            return symbol;
        }

        public X8664Symbol Message(string text)
        {
            if (!this.Messages.TryGetValue(text, out X8664Symbol? symbol))
            {
                symbol = AddReadOnly($"message.{this.Messages.Count}", [.. Encoding.UTF8.GetBytes(text), 0], 1);
                this.Messages[text] = symbol;
            }

            return symbol;
        }

        public X8664Symbol FunctionSymbol(IRFunction function)
        {
            return this.Functions.TryGetValue(function, out X8664Symbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Function '{function.Name}' is not part of the module being translated.");
        }

        public X8664Symbol ImportSymbol(IRFunctionImport functionImport)
        {
            return this.Imports.TryGetValue(functionImport, out X8664Symbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Import '{functionImport.ModuleName}.{functionImport.Name}' is not part of the module being translated.");
        }

        public X8664Symbol GlobalSymbol(IRGlobal global)
        {
            return this.Globals.TryGetValue(global, out X8664Symbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Global '{global.Name}' is not part of the module being translated.");
        }

        public X8664Symbol ConstantSymbol(IRValueInstruction constant)
        {
            return this.Constants.TryGetValue(constant, out X8664Symbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Constant '{constant.Dump(0)}' is not part of the module being translated.");
        }
    }
}
