using CommonIR.Errors;
using CommonIR.Generators.Binary.Xtensa.Emission;
using CommonIR.Generators.Binary.Xtensa.Model;
using CommonIR.Generators.Binary.Xtensa.Runtime;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CommonIR.Generators.Binary.Xtensa.Emission.XtensaRegister;

namespace CommonIR.Generators.Binary.Xtensa.Translation
{
    internal class XtensaTranslator
    {
        const int VectorTableSize = 0x400;
        static readonly int[] ExceptionVectors = [0x300, 0x340, 0x3C0];

        IRModule Module { get; }

        CommonIRXtensaConfiguration Configuration { get; }

        public int CodeSize { get; private set; }

        public int DataSize { get; private set; }

        public uint HeapSize { get; private set; }

        public XtensaTranslator(IRModule module, CommonIRXtensaConfiguration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        public byte[] Translate()
        {
            XtensaContext context = new XtensaContext(this.Configuration);
            XtensaRuntimeBuilder runtime = new XtensaRuntimeBuilder(this.Module, this.Configuration);
            runtime.Build(context);

            (List<IRFunction> functions, HashSet<IRValueInstruction> constants) = Reachable(context, runtime);

            foreach (IRFunction function in functions)
            {
                context.Functions[function] = new XtensaSymbol(function.Name, XtensaSection.Code);
            }

            foreach (IRGlobal global in this.Module.Globals)
            {
                context.Globals[global] = context.AddData(global.Name ?? "global", new byte[8], 8);
            }

            foreach (IRObject constant in this.Module.Objects)
            {
                switch (constant)
                {
                    case IRConstantString constantString when constants.Contains(constantString):
                        context.Constants[constantString] = context.AddData("string", Encoding.UTF8.GetBytes(constantString.Value), 4);
                        break;

                    case IRConstantBytes constantBytes when constants.Contains(constantBytes):
                        context.Constants[constantBytes] = context.AddData("bytes", constantBytes.Value, 8);
                        break;
                }
            }

            XtensaSymbol guard = context.AddData("__exception_guard", new byte[4], 4);

            foreach (IRFunction function in functions)
            {
                try
                {
                    context.Code.Add((function, new XtensaFunctionEmitter(context, function).Emit()));
                }
                catch (Exception ex)
                {
                    throw ErrorHandler.Create($"Could not translate '{function.Name}' for the ESP32: {ex.Message}");
                }
            }

            XtensaSymbol heapStart = new XtensaSymbol("__heap", XtensaSection.Data);
            (XtensaAssembler boot, XtensaLabel entry, XtensaLabel exception) = BuildBoot(context, runtime, heapStart, guard);

            List<byte> text = new List<byte>(new byte[VectorTableSize]);
            List<(XtensaAssembler Code, int PoolStart, int CodeStart)> placed = new();

            void Place(XtensaAssembler code, XtensaSymbol? symbol)
            {
                while (text.Count % 4 != 0)
                {
                    text.Add(0);
                }

                int poolStart = text.Count;
                text.AddRange(new byte[4 * code.Literals.Count]);

                int codeStart = text.Count;
                text.AddRange(code.Code);

                if (symbol != null)
                {
                    symbol.Offset = codeStart;
                }

                placed.Add((code, poolStart, codeStart));
            }

            Place(boot, null);
            int bootStart = placed[0].CodeStart;

            foreach ((IRFunction function, XtensaAssembler code) in context.Code)
            {
                Place(code, context.FunctionSymbol(function));
            }

            while (text.Count % 16 != 0)
            {
                text.Add(0);
            }

            uint dataBase = this.Configuration.DramBase + (uint)text.Count;
            uint dataEnd = dataBase + (uint)context.Data.Count;
            uint heapBase = (dataEnd + 15) & ~15u;
            heapStart.Offset = (int)(heapBase - dataBase);

            if (dataEnd > this.Configuration.LoadLimit)
            {
                throw ErrorHandler.Create($"The program is too large for the ESP32's memory: {text.Count + context.Data.Count} bytes of code and data, at most {this.Configuration.LoadLimit - this.Configuration.DramBase} fit.");
            }

            if (heapBase + 4096 > this.Configuration.HeapEnd)
            {
                throw ErrorHandler.Create("The program leaves no room for its heap on the ESP32.");
            }

            uint AddressOf(XtensaSymbol symbol)
            {
                if (!symbol.IsBound)
                {
                    throw ErrorHandler.Create($"The symbol '{symbol.Name}' was never defined.");
                }

                return symbol.Section == XtensaSection.Code
                    ? this.Configuration.IramBase + (uint)symbol.Offset
                    : dataBase + (uint)symbol.Offset;
            }

            void Patch(int position, int word)
            {
                text[position] = (byte)word;
                text[position + 1] = (byte)(word >> 8);
                text[position + 2] = (byte)(word >> 16);
            }

            int Read(int position) => text[position] | text[position + 1] << 8 | text[position + 2] << 16;

            foreach ((XtensaAssembler code, int poolStart, int codeStart) in placed)
            {
                for (int i = 0; i < code.Literals.Count; i++)
                {
                    XtensaLiteral literal = code.Literals[i];
                    uint value = literal.Symbol != null ? AddressOf(literal.Symbol) + (uint)literal.Value : (uint)literal.Value;

                    for (int b = 0; b < 4; b++)
                    {
                        text[poolStart + 4 * i + b] = (byte)(value >> (8 * b));
                    }
                }

                foreach ((int position, int index) in code.LiteralLoads)
                {
                    int pc = codeStart + position;
                    int literalAddress = poolStart + 4 * index;
                    int offset = (literalAddress - ((pc + 3) & ~3)) >> 2;

                    if (offset >= 0 || offset < -65536)
                    {
                        throw ErrorHandler.Create("A literal is out of reach of its L32R.");
                    }

                    Patch(pc, (Read(pc) & 0xFF) | (offset & 0xFFFF) << 8);
                }

                foreach ((int position, XtensaSymbol target) in code.CallFixups)
                {
                    int pc = codeStart + position;
                    long destination = AddressOf(target) - this.Configuration.IramBase;

                    if (destination % 4 != 0)
                    {
                        throw ErrorHandler.Create($"The call target '{target.Name}' is not aligned.");
                    }

                    long offset = (destination - ((pc & ~3) + 4)) >> 2;

                    if (offset < -(1 << 17) || offset >= (1 << 17))
                    {
                        throw ErrorHandler.Create($"The call to '{target.Name}' is out of range.");
                    }

                    Patch(pc, (int)((offset & 0x3FFFF) << 6) | 5);
                }
            }

            int exceptionAddress = bootStart + exception.Position;

            foreach (int vector in ExceptionVectors)
            {
                int offset = exceptionAddress - (vector + 4);
                Patch(vector, (offset & 0x3FFFF) << 6 | 6);
            }

            this.CodeSize = text.Count;
            this.DataSize = context.Data.Count;
            this.HeapSize = this.Configuration.HeapEnd - heapBase;

            Esp32Image image = new Esp32Image
            {
                EntryPoint = this.Configuration.IramBase + (uint)(bootStart + entry.Position),
            };

            image.Segments.Add((this.Configuration.IramBase, text.ToArray()));

            if (context.Data.Count > 0)
            {
                image.Segments.Add((dataBase, context.Data.ToArray()));
            }

            return image.Serialize();
        }

        (List<IRFunction>, HashSet<IRValueInstruction>) Reachable(XtensaContext context, XtensaRuntimeBuilder runtime)
        {
            HashSet<IRFunction> reached = new HashSet<IRFunction>(ReferenceEqualityComparer.Instance);
            HashSet<IRValueInstruction> constants = new HashSet<IRValueInstruction>(ReferenceEqualityComparer.Instance);
            Queue<IRFunction> pending = new Queue<IRFunction>();
            bool usesDouble = false;

            void Reach(IRFunction? function)
            {
                if (function != null && function is not IRFunctionImport && reached.Add(function))
                {
                    pending.Enqueue(function);
                }
            }

            void VisitBlock(IRBlock block)
            {
                foreach (IRInstruction inner in block.Instructions)
                {
                    Visit(inner);
                }
            }

            void Visit(IRInstruction instruction)
            {
                switch (instruction)
                {
                    case IRCall call when call.Function is IRFunctionImport functionImport:
                        if (context.RuntimeImports.TryGetValue(functionImport, out IRFunction? runtimeFunction))
                        {
                            Reach(runtimeFunction);
                        }
                        break;

                    case IRCall call:
                        Reach(call.Function);
                        break;

                    case IR.Grammar.Instructions.ControlFlow.IRFunctionReference reference:
                        Reach(reference.Function);
                        break;

                    case IRConstantString or IRConstantBytes:
                        constants.Add((IRValueInstruction)instruction);
                        break;
                }

                if (instruction is IRValueInstruction value && XtensaLayout.Classify(value.ValueType) == XtensaValueClass.Double
                    || instruction is IR.Grammar.Instructions.Numeric.IRMath { Operation: IR.Grammar.Instructions.Numeric.IRMathOperation.SquareRoot })
                {
                    usesDouble = true;
                }

                foreach (IRBlock nested in Passes.Optimization.IRTree.NestedBlocks(instruction))
                {
                    VisitBlock(nested);
                }

                foreach (IR.Grammar.Instructions.IRValueInstruction child in Passes.Optimization.IRTree.Children(instruction))
                {
                    Visit(child);
                }
            }

            Reach(runtime.Start);
            Reach(runtime.Exception);
            Reach(context.Malloc);
            Reach(context.Free);
            Reach(context.Panic);
            Reach(context.Divide64);
            Reach(context.DivideUnsigned64);
            Reach(context.Remainder64);
            Reach(context.RemainderUnsigned64);

            bool rootedSoftFloat = false;

            while (pending.Count > 0)
            {
                VisitBlock(pending.Dequeue().Entryblock);

                if (pending.Count == 0 && usesDouble && !rootedSoftFloat && context.SoftFloat is XtensaSoftFloat soft)
                {
                    rootedSoftFloat = true;

                    foreach (IRFunction function in soft.All)
                    {
                        Reach(function);
                    }
                }
            }

            if (!rootedSoftFloat)
            {
                context.SoftFloat = null;
            }

            return (this.Module.Functions.Where(reached.Contains).ToList(), constants);
        }

        (XtensaAssembler, XtensaLabel, XtensaLabel) BuildBoot(XtensaContext context, XtensaRuntimeBuilder runtime, XtensaSymbol heapStart, XtensaSymbol guard)
        {
            XtensaAssembler a = new XtensaAssembler();
            XtensaLabel entry = new XtensaLabel("entry");
            XtensaLabel halt = new XtensaLabel("halt");
            XtensaLabel exception = new XtensaLabel("exception");

            void Store(uint address, uint value)
            {
                a.LoadConstant(A8, address);
                a.LoadConstant(A9, value);
                a.Memw();
                a.S32i(A9, A8, 0);
            }

            a.Bind(entry);
            a.LoadConstant(A2, 0x2F);
            a.Wsr(A2, XtensaSpecialRegister.PS);
            a.Rsync();
            a.LoadConstant(A1, this.Configuration.StackTop);
            a.LoadConstant(A2, this.Configuration.IramBase);
            a.Wsr(A2, XtensaSpecialRegister.VECBASE);
            a.Rsync();

            Store(0x600080B0, 0x50D83AA1);
            Store(0x60008098, 0);
            Store(0x600080B0, 0);

            Store(0x600080B8, 0x8F1D312A);
            a.LoadConstant(A8, 0x600080B4);
            a.L32i(A9, A8, 0);
            a.LoadConstant(A10, unchecked((int)0x80000000));
            a.Or(A9, A9, A10);
            a.S32i(A9, A8, 0);
            Store(0x600080B8, 0);

            foreach (uint group in new uint[] { 0x6001F000, 0x60020000 })
            {
                Store(group + 0x64, 0x50D83AA1);
                Store(group + 0x48, 0);
                Store(group + 0x64, 0);
            }

            a.LoadConstant(A8, 0x600C0010);
            a.L32i(A9, A8, 0);
            a.Movi(A10, -8);
            a.And(A9, A9, A10);
            a.Movi(A10, 5);
            a.Or(A9, A9, A10);
            a.Memw();
            a.S32i(A9, A8, 0);
            a.LoadConstant(A8, 0x600C0060);
            a.L32i(A9, A8, 0);
            a.LoadConstant(A10, unchecked((int)0xFFFFF3FF));
            a.And(A9, A9, A10);
            a.LoadConstant(A10, 0x400);
            a.Or(A9, A9, A10);
            a.Memw();
            a.S32i(A9, A8, 0);

            a.Movi(A2, 1);
            a.Wsr(A2, XtensaSpecialRegister.CPENABLE);
            a.Rsync();

            a.LoadAddress(A2, heapStart);
            a.LoadAddress(A3, context.GlobalSymbol(runtime.HeapNext));
            a.S32i(A2, A3, 0);
            a.LoadConstant(A2, this.Configuration.HeapEnd);
            a.LoadAddress(A3, context.GlobalSymbol(runtime.HeapEnd));
            a.S32i(A2, A3, 0);

            a.Call0(context.FunctionSymbol(runtime.Start));

            a.Bind(halt);
            a.Jump(halt);

            a.Align(4);
            a.Bind(exception);
            a.LoadAddress(A8, guard);
            a.L32i(A9, A8, 0);
            a.BranchZero(false, A9, halt);
            a.Movi(A9, 1);
            a.S32i(A9, A8, 0);
            a.LoadConstant(A1, this.Configuration.StackTop);
            a.Rsr(A2, XtensaSpecialRegister.EXCCAUSE);
            a.Rsr(A3, XtensaSpecialRegister.EPC1);
            a.Rsr(A4, XtensaSpecialRegister.EXCVADDR);
            a.Call0(context.FunctionSymbol(runtime.Exception));
            a.Jump(halt);

            a.ResolveLocal();

            return (a, entry, exception);
        }
    }
}
