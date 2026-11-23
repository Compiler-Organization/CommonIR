using CommonIR.Errors;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.Xtensa.Emission
{
    internal enum XtensaIntrinsic
    {
        Read32,
        Write32,
        Read8,
        Write8,
        Cycles,
        Address,
        Barrier,
    }

    internal class XtensaContext
    {
        public CommonIRXtensaConfiguration Configuration { get; }

        public Dictionary<IRFunction, XtensaSymbol> Functions { get; } = new Dictionary<IRFunction, XtensaSymbol>();

        public Dictionary<IRFunctionImport, IRFunction> RuntimeImports { get; } = new Dictionary<IRFunctionImport, IRFunction>();

        public Dictionary<IRFunctionImport, XtensaIntrinsic> Intrinsics { get; } = new Dictionary<IRFunctionImport, XtensaIntrinsic>();

        public Dictionary<IRGlobal, XtensaSymbol> Globals { get; } = new Dictionary<IRGlobal, XtensaSymbol>();

        public Dictionary<IRValueInstruction, XtensaSymbol> Constants { get; } = new Dictionary<IRValueInstruction, XtensaSymbol>();

        public List<byte> Data { get; } = new List<byte>();

        Dictionary<string, XtensaSymbol> Messages { get; } = new Dictionary<string, XtensaSymbol>(StringComparer.Ordinal);

        public List<(IRFunction Function, XtensaAssembler Code)> Code { get; } = new List<(IRFunction Function, XtensaAssembler Code)>();

        public IRFunction Malloc { get; set; } = null!;

        public IRFunction Free { get; set; } = null!;

        public IRFunction Panic { get; set; } = null!;

        public IRFunction Divide64 { get; set; } = null!;

        public IRFunction DivideUnsigned64 { get; set; } = null!;

        public IRFunction Remainder64 { get; set; } = null!;

        public IRFunction RemainderUnsigned64 { get; set; } = null!;

        public XtensaSoftFloat? SoftFloat { get; set; }

        public XtensaContext(CommonIRXtensaConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public XtensaSymbol AddData(string name, byte[] bytes, int alignment)
        {
            while (this.Data.Count % alignment != 0)
            {
                this.Data.Add(0);
            }

            XtensaSymbol symbol = new XtensaSymbol(name, XtensaSection.Data, this.Data.Count);
            this.Data.AddRange(bytes);

            return symbol;
        }

        public XtensaSymbol Message(string text)
        {
            if (!this.Messages.TryGetValue(text, out XtensaSymbol? symbol))
            {
                symbol = AddData($"message.{this.Messages.Count}", Encoding.UTF8.GetBytes(text), 1);
                this.Messages[text] = symbol;
            }

            return symbol;
        }

        public XtensaSymbol FunctionSymbol(IRFunction function)
        {
            return this.Functions.TryGetValue(function, out XtensaSymbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Function '{function.Name}' is not part of the module being translated.");
        }

        public XtensaSymbol GlobalSymbol(IRGlobal global)
        {
            return this.Globals.TryGetValue(global, out XtensaSymbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Global '{global.Name}' is not part of the module being translated.");
        }

        public XtensaSymbol ConstantSymbol(IRValueInstruction constant)
        {
            return this.Constants.TryGetValue(constant, out XtensaSymbol? symbol)
                ? symbol
                : throw ErrorHandler.Create($"Constant '{constant.Dump(0)}' is not part of the module being translated.");
        }
    }
}
