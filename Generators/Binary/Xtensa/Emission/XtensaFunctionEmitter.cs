using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CommonIR.Generators.Binary.Xtensa.Emission.XtensaRegister;

namespace CommonIR.Generators.Binary.Xtensa.Emission
{
    internal class XtensaFunctionEmitter
    {
        public const string DivideByZeroMessage = "Attempted to divide by zero.";
        public const string IndexOutOfRangeMessage = "Index was outside the bounds of the array.";
        public const string NullReferenceMessage = "Object reference not set to an instance of an object.";
        public const string NegativeSizeMessage = "Arrays cannot have a negative size.";
        public const string StackOverflowMessage = "Stack overflow.";

        const int ArgumentRegisters = 6;

        static readonly XtensaRegister[] CalleeSaved = [A10, A11, A12, A13, A14, A15];

        sealed class Temporary
        {
            public XtensaValueClass Class { get; init; }

            public bool InRegister { get; set; }

            public XtensaRegister Low { get; set; }

            public XtensaRegister High { get; set; }

            public int Slot { get; set; } = -1;
        }

        sealed class ArraySource
        {
            public int Slot { get; init; } = -1;

            public Temporary? Temporary { get; init; }
        }

        XtensaContext Context { get; }

        IRFunction Function { get; }

        XtensaAssembler Assembler { get; set; } = null!;

        Dictionary<IRLocal, int> LocalSlots { get; set; } = new Dictionary<IRLocal, int>();

        Dictionary<IRInstruction, int> ResultSlots { get; set; } = new Dictionary<IRInstruction, int>();

        Stack<int> FreeSlots { get; set; } = new Stack<int>();

        List<XtensaRegister> Pool { get; set; } = new List<XtensaRegister>();

        HashSet<XtensaRegister> UsedPool { get; set; } = new HashSet<XtensaRegister>();

        Stack<(XtensaLabel Continue, XtensaLabel Exit)> Loops { get; set; } = new Stack<(XtensaLabel Continue, XtensaLabel Exit)>();

        Dictionary<string, XtensaLabel> PanicStubs { get; set; } = new Dictionary<string, XtensaLabel>(StringComparer.Ordinal);

        XtensaLabel Epilogue { get; set; } = null!;

        XtensaLabel StackOverflow { get; set; } = null!;

        int SlotCount { get; set; }

        int MaxOutgoing { get; set; }

        int LabelCount { get; set; }

        int AssumedSlots { get; set; }

        int AssumedOutgoing { get; set; }

        List<XtensaRegister> AssumedSaved { get; set; } = new List<XtensaRegister>();

        int FrameSize => XtensaLayout.AlignUp(this.AssumedOutgoing + 8 * this.AssumedSlots + 4 * this.AssumedSaved.Count + 4, 16);

        public XtensaFunctionEmitter(XtensaContext context, IRFunction function)
        {
            this.Context = context;
            this.Function = function;
        }

        public XtensaAssembler Emit()
        {
            HashSet<int> longBranches = new HashSet<int>();

            for (int attempt = 0; attempt < 64; attempt++)
            {
                XtensaAssembler assembler = new XtensaAssembler { LongBranches = longBranches };
                EmitPass(assembler);

                List<XtensaRegister> saved = CalleeSaved.Where(this.UsedPool.Contains).ToList();
                bool frameChanged = this.SlotCount > this.AssumedSlots || this.MaxOutgoing > this.AssumedOutgoing || !saved.SequenceEqual(this.AssumedSaved);

                if (assembler.FailedBranches.Count == 0 && !frameChanged)
                {
                    return assembler;
                }

                longBranches.UnionWith(assembler.FailedBranches);
                this.AssumedSlots = Math.Max(this.AssumedSlots, this.SlotCount);
                this.AssumedOutgoing = Math.Max(this.AssumedOutgoing, this.MaxOutgoing);
                this.AssumedSaved = CalleeSaved.Where(r => saved.Contains(r) || this.AssumedSaved.Contains(r)).ToList();
            }

            throw ErrorHandler.Create($"Could not lay out '{this.Function.Name}'.");
        }

        void EmitPass(XtensaAssembler assembler)
        {
            this.Assembler = assembler;
            this.LocalSlots = new Dictionary<IRLocal, int>();
            this.ResultSlots = new Dictionary<IRInstruction, int>();
            this.FreeSlots = new Stack<int>();
            this.Loops = new Stack<(XtensaLabel Continue, XtensaLabel Exit)>();
            this.PanicStubs = new Dictionary<string, XtensaLabel>(StringComparer.Ordinal);
            this.Pool = CalleeSaved.Reverse().ToList();
            this.UsedPool = new HashSet<XtensaRegister>();
            this.SlotCount = 0;
            this.MaxOutgoing = 0;
            this.LabelCount = 0;
            this.Epilogue = NewLabel("epilogue");
            this.StackOverflow = NewLabel("overflow");

            int frame = this.FrameSize;

            this.Assembler.AddConstant(A1, A1, -frame, A8);
            this.Assembler.LoadConstant(A8, this.Context.Configuration.StackLimit);
            this.Assembler.Branch(XtensaBranch.LessUnsigned, A1, A8, this.StackOverflow);

            StoreFrame(A0, frame - 4);

            for (int i = 0; i < this.AssumedSaved.Count; i++)
            {
                StoreFrame(this.AssumedSaved[i], frame - 8 - 4 * i);
            }

            SpillParameters(frame);
            EmitBlock(this.Function.Entryblock);

            this.Assembler.Bind(this.Epilogue);

            for (int i = 0; i < this.AssumedSaved.Count; i++)
            {
                LoadFrame(this.AssumedSaved[i], frame - 8 - 4 * i);
            }

            LoadFrame(A0, frame - 4);
            this.Assembler.AddConstant(A1, A1, frame, A8);
            this.Assembler.Ret();

            EmitPanicStubs();

            this.Assembler.Bind(this.StackOverflow);
            this.Assembler.LoadConstant(A1, this.Context.Configuration.StackTop);
            this.Assembler.LoadAddress(A2, this.Context.Message(StackOverflowMessage));
            this.Assembler.LoadConstant(A3, Encoding.UTF8.GetByteCount(StackOverflowMessage));
            this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Panic));

            this.Assembler.ResolveLocal();
        }

        #region Frame

        XtensaLabel NewLabel(string name) => new XtensaLabel($"{this.Function.Name}.{name}.{this.LabelCount++}");

        int SlotOffset(int slot) => this.AssumedOutgoing + 8 * slot;

        int NewSlot() => this.SlotCount++;

        int SlotOf(IRLocal local)
        {
            if (!this.LocalSlots.TryGetValue(local, out int slot))
            {
                slot = NewSlot();
                this.LocalSlots[local] = slot;
            }

            return slot;
        }

        (XtensaRegister Base, int Displacement) FrameAddress(int offset, int size)
        {
            if (XtensaAssembler.FitsLoad(4, offset) && XtensaAssembler.FitsLoad(4, offset + Math.Max(0, size - 4)))
            {
                return (A1, offset);
            }

            this.Assembler.AddConstant(A9, A1, offset, A9);
            return (A9, 0);
        }

        void LoadFrame(XtensaRegister register, int offset)
        {
            (XtensaRegister b, int d) = FrameAddress(offset, 4);
            this.Assembler.L32i(register, b, d);
        }

        void StoreFrame(XtensaRegister register, int offset)
        {
            (XtensaRegister b, int d) = FrameAddress(offset, 4);
            this.Assembler.S32i(register, b, d);
        }

        void LoadSlot(int slot, XtensaValueClass valueClass, XtensaRegister low, XtensaRegister high)
        {
            (XtensaRegister b, int d) = FrameAddress(SlotOffset(slot), 8);
            this.Assembler.L32i(low, b, d);

            if (XtensaLayout.Words(valueClass) == 2)
            {
                this.Assembler.L32i(high, b, d + 4);
            }
        }

        void StoreSlot(int slot, XtensaValueClass valueClass, XtensaRegister low = A2, XtensaRegister high = A3)
        {
            (XtensaRegister b, int d) = FrameAddress(SlotOffset(slot), 8);
            this.Assembler.S32i(low, b, d);

            if (XtensaLayout.Words(valueClass) == 2)
            {
                this.Assembler.S32i(high, b, d + 4);
            }
        }

        Temporary AllocateTemporary(XtensaValueClass valueClass)
        {
            Temporary temporary = new Temporary { Class = valueClass };
            int words = XtensaLayout.Words(valueClass);

            if (this.Pool.Count >= words)
            {
                temporary.InRegister = true;
                temporary.Low = TakeRegister();

                if (words == 2)
                {
                    temporary.High = TakeRegister();
                }
            }
            else
            {
                temporary.Slot = this.FreeSlots.Count > 0 ? this.FreeSlots.Pop() : NewSlot();
            }

            return temporary;
        }

        XtensaRegister TakeRegister()
        {
            XtensaRegister register = this.Pool[^1];
            this.Pool.RemoveAt(this.Pool.Count - 1);
            this.UsedPool.Add(register);
            return register;
        }

        void Release(Temporary? temporary)
        {
            if (temporary == null)
            {
                return;
            }

            if (!temporary.InRegister)
            {
                this.FreeSlots.Push(temporary.Slot);
                return;
            }

            if (XtensaLayout.Words(temporary.Class) == 2)
            {
                this.Pool.Add(temporary.High);
            }

            this.Pool.Add(temporary.Low);
        }

        Temporary SaveTemporary(XtensaValueClass valueClass)
        {
            Temporary temporary = AllocateTemporary(valueClass);

            if (!temporary.InRegister)
            {
                StoreSlot(temporary.Slot, valueClass);
                return temporary;
            }

            this.Assembler.Mov(temporary.Low, A2);

            if (XtensaLayout.Words(valueClass) == 2)
            {
                this.Assembler.Mov(temporary.High, A3);
            }

            return temporary;
        }

        void LoadTemporary(Temporary temporary, XtensaRegister low = A2, XtensaRegister high = A3)
        {
            if (!temporary.InRegister)
            {
                LoadSlot(temporary.Slot, temporary.Class, low, high);
                return;
            }

            this.Assembler.Mov(low, temporary.Low);

            if (XtensaLayout.Words(temporary.Class) == 2)
            {
                this.Assembler.Mov(high, temporary.High);
            }
        }

        XtensaLabel PanicStub(string message)
        {
            if (!this.PanicStubs.TryGetValue(message, out XtensaLabel? label))
            {
                label = NewLabel("panic");
                this.PanicStubs[message] = label;
            }

            return label;
        }

        void EmitPanicStubs()
        {
            foreach ((string message, XtensaLabel label) in this.PanicStubs)
            {
                this.Assembler.Bind(label);
                this.Assembler.LoadAddress(A2, this.Context.Message(message));
                this.Assembler.LoadConstant(A3, Encoding.UTF8.GetByteCount(message));
                this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Panic));
            }
        }

        void SpillParameters(int frame)
        {
            int word = 0;

            foreach (IRLocal parameter in this.Function.Parameters)
            {
                XtensaValueClass valueClass = XtensaLayout.Classify(parameter.ValueType);
                int slot = SlotOf(parameter);

                for (int part = 0; part < XtensaLayout.Words(valueClass); part++, word++)
                {
                    XtensaRegister source;

                    if (word < ArgumentRegisters)
                    {
                        source = (XtensaRegister)((int)A2 + word);
                    }
                    else
                    {
                        LoadFrame(A8, frame + 4 * (word - ArgumentRegisters));
                        source = A8;
                    }

                    (XtensaRegister b, int d) = FrameAddress(SlotOffset(slot) + 4 * part, 4);
                    this.Assembler.S32i(source, b, d);
                }
            }
        }

        #endregion

        #region Value movement

        void Normalize(IRType type, XtensaRegister register = A2)
        {
            switch (type.DataType)
            {
                case IRDataTypes.Int8:
                    this.Assembler.Sext(register, register, 7);
                    break;

                case IRDataTypes.UInt8:
                case IRDataTypes.Bool:
                    this.Assembler.Extui(register, register, 0, 8);
                    break;

                case IRDataTypes.Int16:
                    this.Assembler.Sext(register, register, 15);
                    break;

                case IRDataTypes.UInt16:
                    this.Assembler.Extui(register, register, 0, 16);
                    break;
            }
        }

        void Coerce(IRType from, IRType to, XtensaRegister low = A2, XtensaRegister high = A3)
        {
            XtensaValueClass source = XtensaLayout.Classify(from);
            XtensaValueClass target = XtensaLayout.Classify(to);

            if (source == XtensaValueClass.Word && target == XtensaValueClass.Pair)
            {
                Extend(XtensaLayout.IsSigned(from), low, high);
            }
        }

        void Extend(bool signed, XtensaRegister low = A2, XtensaRegister high = A3)
        {
            if (signed)
            {
                this.Assembler.Srai(high, low, 31);
            }
            else
            {
                this.Assembler.Movi(high, 0);
            }
        }

        bool IsSimple(IRValueInstruction value)
        {
            if (this.ResultSlots.ContainsKey(value))
            {
                return true;
            }

            return value is IRConstantInteger or IRLocal or IRGlobal or IRStructProperty or IRConstantString or IRConstantBytes;
        }

        void LoadSimple(IRValueInstruction value, XtensaRegister low, XtensaRegister high)
        {
            if (this.ResultSlots.TryGetValue(value, out int resultSlot))
            {
                LoadSlot(resultSlot, XtensaLayout.Classify(value.ValueType), low, high);
                return;
            }

            switch (value)
            {
                case IRConstantInteger constant:
                    {
                        long bits = XtensaLayout.Bits(constant.IntegerType, constant.Value);
                        this.Assembler.LoadConstant(low, bits);

                        if (XtensaLayout.Words(constant.ValueType) == 2)
                        {
                            this.Assembler.LoadConstant(high, bits >> 32);
                        }
                    }
                    break;

                case IRConstantString constantString:
                    this.Assembler.LoadAddress(low, this.Context.ConstantSymbol(constantString));
                    this.Assembler.LoadConstant(high, Encoding.UTF8.GetByteCount(constantString.Value));
                    break;

                case IRConstantBytes constantBytes:
                    this.Assembler.LoadAddress(low, this.Context.ConstantSymbol(constantBytes));
                    this.Assembler.LoadConstant(high, constantBytes.Value.Length);
                    break;

                case IRLocal local:
                    LoadSlot(SlotOf(local), XtensaLayout.Classify(local.ValueType), low, high);
                    break;

                case IRGlobal global:
                    this.Assembler.LoadAddress(A8, this.Context.GlobalSymbol(global));
                    this.Assembler.L32i(low, A8, 0);

                    if (XtensaLayout.Words(global.ValueType) == 2)
                    {
                        this.Assembler.L32i(high, A8, 4);
                    }
                    break;

                case IRStructProperty property:
                    this.Assembler.LoadConstant(low, XtensaLayout.OffsetOf(property));
                    break;

                default:
                    throw ErrorHandler.Create($"'{value.Dump(0)}' is not a simple value.");
            }
        }

        void EmitOperands(IRValueInstruction left, IRValueInstruction right, bool convertRight = true)
        {
            if (IsSimple(right))
            {
                EmitValue(left);
                LoadSimple(right, A4, A5);
            }
            else
            {
                EmitValue(left);
                Temporary temporary = SaveTemporary(XtensaLayout.Classify(left.ValueType));

                EmitValue(right);
                this.Assembler.Mov(A4, A2);
                this.Assembler.Mov(A5, A3);

                LoadTemporary(temporary);
                Release(temporary);
            }

            if (convertRight)
            {
                Coerce(right.ValueType, left.ValueType, A4, A5);
            }
        }

        void LoadTyped(IRType type, XtensaRegister address, int displacement)
        {
            (XtensaRegister b, int d) = Fit(address, displacement, Math.Min(4, XtensaLayout.SizeOf(type)), XtensaLayout.Words(type) == 2 ? 4 : 0);

            switch (XtensaLayout.Classify(type))
            {
                case XtensaValueClass.Word:
                case XtensaValueClass.Single:
                    this.Assembler.Load(XtensaLayout.SizeOf(type), XtensaLayout.IsSigned(type), A2, b, d);

                    if (type.DataType == IRDataTypes.Bool)
                    {
                        Normalize(type);
                    }
                    break;

                case XtensaValueClass.Pair:
                case XtensaValueClass.Double:
                case XtensaValueClass.Fat:
                    if (b == A2)
                    {
                        this.Assembler.L32i(A3, b, d + 4);
                        this.Assembler.L32i(A2, b, d);
                    }
                    else
                    {
                        this.Assembler.L32i(A2, b, d);
                        this.Assembler.L32i(A3, b, d + 4);
                    }
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot load a value of type '{type.Dump(0)}' from memory.");
            }
        }

        void StoreTyped(IRType type, XtensaRegister address, int displacement)
        {
            (XtensaRegister b, int d) = Fit(address, displacement, Math.Min(4, XtensaLayout.SizeOf(type)), XtensaLayout.Words(type) == 2 ? 4 : 0);

            switch (XtensaLayout.Classify(type))
            {
                case XtensaValueClass.Word:
                case XtensaValueClass.Single:
                    this.Assembler.Store(XtensaLayout.SizeOf(type), A2, b, d);
                    break;

                case XtensaValueClass.Pair:
                case XtensaValueClass.Double:
                case XtensaValueClass.Fat:
                    this.Assembler.S32i(A2, b, d);
                    this.Assembler.S32i(A3, b, d + 4);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot store a value of type '{type.Dump(0)}' to memory.");
            }
        }

        (XtensaRegister, int) Fit(XtensaRegister address, int displacement, int size, int extra)
        {
            if (XtensaAssembler.FitsLoad(size, displacement) && XtensaAssembler.FitsLoad(size, displacement + extra))
            {
                return (address, displacement);
            }

            this.Assembler.AddConstant(A9, address, displacement, A9);
            return (A9, 0);
        }

        #endregion

        #region Statements

        void EmitBlock(IRBlock block)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                EmitStatement(instruction);
            }
        }

        void EmitStatement(IRInstruction instruction)
        {
            switch (instruction)
            {
                case IRStore store:
                    EmitStore(store);
                    break;

                case IRStoreElement storeElement:
                    EmitStoreElement(storeElement);
                    break;

                case IRReturn returnInstruction:
                    if (returnInstruction.Values != null && returnInstruction.Values.Count > 0)
                    {
                        IRValueInstruction value = returnInstruction.Values[0];
                        EmitValue(value);

                        if (this.Function.ReturnTypes.Count > 0)
                        {
                            Coerce(value.ValueType, this.Function.ReturnTypes[0]);
                        }
                    }

                    this.Assembler.Jump(this.Epilogue);
                    break;

                case IRConditionalBranch conditionalBranch:
                    EmitConditionalBranch(conditionalBranch);
                    break;

                case IRLoop loop:
                    EmitLoop(loop);
                    break;

                case IRBreak:
                    if (this.Loops.Count == 0)
                    {
                        throw ErrorHandler.Create($"'break' used outside of a loop in '{this.Function.Name}'.");
                    }

                    this.Assembler.Jump(this.Loops.Peek().Exit);
                    break;

                case IRContinue:
                    if (this.Loops.Count == 0)
                    {
                        throw ErrorHandler.Create($"'continue' used outside of a loop in '{this.Function.Name}'.");
                    }

                    this.Assembler.Jump(this.Loops.Peek().Continue);
                    break;

                case IRPanic panic:
                    EmitValue(panic.Message);
                    this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Panic));
                    break;

                case IRFree free:
                    EmitValue(free.Pointer);
                    this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Free));
                    break;

                case IRBlock block:
                    EmitBlock(block);
                    break;

                case IRCall or IRCallIndirect:
                    {
                        IRValueInstruction invocation = (IRValueInstruction)instruction;
                        EmitInvocation(invocation);

                        XtensaValueClass valueClass = XtensaLayout.Classify(invocation.ValueType);

                        if (valueClass != XtensaValueClass.Void)
                        {
                            if (!this.ResultSlots.TryGetValue(invocation, out int slot))
                            {
                                slot = NewSlot();
                                this.ResultSlots[invocation] = slot;
                            }

                            StoreSlot(slot, valueClass);
                        }
                    }
                    break;

                case IRDispatch or IRAwait:
                    throw ErrorHandler.Create("Parallel functions (GPU kernels) are not supported on the ESP32.");

                case IRBytes:
                    throw ErrorHandler.Create("Raw machine code is not supported on the ESP32.");

                case IRValueInstruction value:
                    EmitValue(value);
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"No Xtensa translation implemented for instruction '{instruction.GetType().Name}'");
            }
        }

        void EmitConditionalBranch(IRConditionalBranch conditionalBranch)
        {
            XtensaLabel elseLabel = NewLabel("else");
            XtensaLabel endLabel = NewLabel("endif");

            EmitBranch(conditionalBranch.Condition, false, conditionalBranch.HasElseBlock ? elseLabel : endLabel);
            EmitBlock(conditionalBranch.ThenBlock);

            if (conditionalBranch.HasElseBlock)
            {
                this.Assembler.Jump(endLabel);
                this.Assembler.Bind(elseLabel);
                EmitBlock(conditionalBranch.ElseBlock);
            }

            this.Assembler.Bind(endLabel);
        }

        void EmitLoop(IRLoop loop)
        {
            XtensaLabel topLabel = NewLabel("loop");
            XtensaLabel continueLabel = NewLabel("continue");
            XtensaLabel checkLabel = NewLabel("check");
            XtensaLabel exitLabel = NewLabel("exit");

            bool infinite = loop.Condition is IRConstantInteger { Value: not 0 };

            if (!infinite)
            {
                this.Assembler.Jump(checkLabel);
            }

            this.Assembler.Bind(topLabel);

            this.Loops.Push((continueLabel, exitLabel));
            EmitBlock(loop.Block);
            this.Loops.Pop();

            this.Assembler.Bind(continueLabel);

            if (loop.Increment != null)
            {
                EmitBlock(loop.Increment);
            }

            this.Assembler.Bind(checkLabel);

            if (infinite)
            {
                this.Assembler.Jump(topLabel);
            }
            else
            {
                EmitBranch(loop.Condition, true, topLabel);
            }

            this.Assembler.Bind(exitLabel);
        }

        static XtensaBranch? BranchCondition(IRComparisonOperator comparison, bool signed, out bool swap)
        {
            swap = comparison is IRComparisonOperator.GreaterThan or IRComparisonOperator.LessThanOrEqual;

            return comparison switch
            {
                IRComparisonOperator.EqualTo => XtensaBranch.Equal,
                IRComparisonOperator.NotEqualTo => XtensaBranch.NotEqual,
                IRComparisonOperator.LessThan or IRComparisonOperator.GreaterThan => signed ? XtensaBranch.Less : XtensaBranch.LessUnsigned,
                IRComparisonOperator.GreaterThanOrEqual or IRComparisonOperator.LessThanOrEqual => signed ? XtensaBranch.GreaterOrEqual : XtensaBranch.GreaterOrEqualUnsigned,
                _ => null,
            };
        }

        void EmitBranch(IRValueInstruction condition, bool when, XtensaLabel target)
        {
            if (!this.ResultSlots.ContainsKey(condition))
            {
                switch (condition)
                {
                    case IRConstantInteger constant:
                        if ((constant.Value != 0) == when)
                        {
                            this.Assembler.Jump(target);
                        }
                        return;

                    case IRCompare { Operator: IRComparisonOperator.EqualTo, Right: IRConstantInteger { Value: 0 }, Left: IRCompare inner } compare when compare.OperandType.DataType == IRDataTypes.Bool:
                        EmitBranch(inner, !when, target);
                        return;

                    case IRCompare compare:
                        EmitCompareBranch(compare, when, target);
                        return;
                }
            }

            EmitValue(condition);
            TestNonZero(condition.ValueType, when, target);
        }

        void TestNonZero(IRType type, bool when, XtensaLabel target)
        {
            switch (XtensaLayout.Classify(type))
            {
                case XtensaValueClass.Pair:
                case XtensaValueClass.Fat:
                    this.Assembler.Or(A2, A2, A3);
                    break;

                case XtensaValueClass.Single:
                    this.Assembler.Slli(A2, A2, 1);
                    break;

                case XtensaValueClass.Double:
                    this.Assembler.Slli(A3, A3, 1);
                    this.Assembler.Or(A2, A2, A3);
                    break;
            }

            this.Assembler.BranchZero(!when, A2, target);
        }

        void EmitCompareBranch(IRCompare compare, bool when, XtensaLabel target)
        {
            XtensaValueClass valueClass = XtensaLayout.Classify(compare.OperandType);
            bool signed = XtensaLayout.IsSigned(compare.OperandType);

            switch (valueClass)
            {
                case XtensaValueClass.Word:
                    {
                        XtensaBranch? branch = BranchCondition(compare.Operator, signed, out bool swap);

                        if (branch == null)
                        {
                            throw ErrorHandler.CreateNotImplimented($"Comparison '{compare.Operator}' is not implemented.");
                        }

                        if (compare.Right is IRConstantInteger { Value: 0 } && !this.ResultSlots.ContainsKey(compare.Right))
                        {
                            switch (compare.Operator)
                            {
                                case IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo:
                                    EmitValue(compare.Left);
                                    this.Assembler.BranchZero((compare.Operator == IRComparisonOperator.EqualTo) == when, A2, target);
                                    return;

                                case IRComparisonOperator.LessThan or IRComparisonOperator.GreaterThanOrEqual when signed:
                                    EmitValue(compare.Left);
                                    this.Assembler.BranchNegative((compare.Operator == IRComparisonOperator.LessThan) == when, A2, target);
                                    return;
                            }
                        }

                        EmitOperands(compare.Left, compare.Right);

                        XtensaBranch jump = when ? branch.Value : XtensaAssembler.Invert(branch.Value);

                        if (swap)
                        {
                            this.Assembler.Branch(jump, A4, A2, target);
                        }
                        else
                        {
                            this.Assembler.Branch(jump, A2, A4, target);
                        }
                    }
                    return;

                case XtensaValueClass.Pair:
                    EmitOperands(compare.Left, compare.Right);
                    EmitPairBranch(compare.Operator, signed, when, target);
                    return;

                case XtensaValueClass.Fat:
                    {
                        if (compare.Operator is not (IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo))
                        {
                            throw ErrorHandler.Create($"Only equality comparisons are supported on values of type '{compare.OperandType.Dump(0)}'.");
                        }

                        EmitOperands(compare.Left, compare.Right);
                        EmitPairBranch(compare.Operator, false, when, target);
                    }
                    return;

                case XtensaValueClass.Single:
                    {
                        EmitOperands(compare.Left, compare.Right);
                        bool inverted = EmitSingleCompare(compare.Operator);
                        this.Assembler.BranchBoolean(when != inverted, 0, target);
                    }
                    return;

                case XtensaValueClass.Double:
                    EmitOperands(compare.Left, compare.Right);
                    EmitDoubleCompare(compare.Operator);
                    this.Assembler.BranchZero(!when, A2, target);
                    return;
            }

            throw ErrorHandler.Create($"Cannot compare values of type '{compare.OperandType.Dump(0)}'.");
        }

        void EmitPairBranch(IRComparisonOperator comparison, bool signed, bool when, XtensaLabel target)
        {
            XtensaLabel skip = NewLabel("pair");

            if (comparison is IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo)
            {
                bool jumpWhenEqual = (comparison == IRComparisonOperator.EqualTo) == when;

                if (jumpWhenEqual)
                {
                    this.Assembler.Branch(XtensaBranch.NotEqual, A3, A5, skip);
                    this.Assembler.Branch(XtensaBranch.Equal, A2, A4, target);
                }
                else
                {
                    this.Assembler.Branch(XtensaBranch.NotEqual, A3, A5, target);
                    this.Assembler.Branch(XtensaBranch.NotEqual, A2, A4, target);
                }

                this.Assembler.Bind(skip);
                return;
            }

            bool swap = comparison is IRComparisonOperator.GreaterThan or IRComparisonOperator.LessThanOrEqual;
            bool strict = comparison is IRComparisonOperator.LessThan or IRComparisonOperator.GreaterThan;

            XtensaRegister leftLow = swap ? A4 : A2, leftHigh = swap ? A5 : A3, rightLow = swap ? A2 : A4, rightHigh = swap ? A3 : A5;

            XtensaLabel isTrue = when ? target : skip;
            XtensaLabel isFalse = when ? skip : target;

            this.Assembler.Branch(signed ? XtensaBranch.Less : XtensaBranch.LessUnsigned, leftHigh, rightHigh, strict ? isTrue : isFalse);
            this.Assembler.Branch(XtensaBranch.NotEqual, leftHigh, rightHigh, strict ? isFalse : isTrue);
            this.Assembler.Branch(XtensaBranch.LessUnsigned, leftLow, rightLow, strict ? isTrue : isFalse);

            if (strict)
            {
                this.Assembler.Jump(isFalse);
            }
            else
            {
                this.Assembler.Jump(isTrue);
            }

            this.Assembler.Bind(skip);
        }

        bool EmitSingleCompare(IRComparisonOperator comparison)
        {
            this.Assembler.Wfr(0, A2);
            this.Assembler.Wfr(1, A4);

            switch (comparison)
            {
                case IRComparisonOperator.EqualTo:
                    this.Assembler.FloatCompare(2, 0, 0, 1);
                    return false;

                case IRComparisonOperator.NotEqualTo:
                    this.Assembler.FloatCompare(2, 0, 0, 1);
                    return true;

                case IRComparisonOperator.LessThan:
                    this.Assembler.FloatCompare(4, 0, 0, 1);
                    return false;

                case IRComparisonOperator.LessThanOrEqual:
                    this.Assembler.FloatCompare(6, 0, 0, 1);
                    return false;

                case IRComparisonOperator.GreaterThan:
                    this.Assembler.FloatCompare(4, 0, 1, 0);
                    return false;

                case IRComparisonOperator.GreaterThanOrEqual:
                    this.Assembler.FloatCompare(6, 0, 1, 0);
                    return false;
            }

            throw ErrorHandler.CreateNotImplimented($"Comparison '{comparison}' is not implemented.");
        }

        void EmitDoubleCompare(IRComparisonOperator comparison)
        {
            XtensaSoftFloat soft = SoftFloat("Comparing f64 values");

            this.Assembler.Call0(this.Context.FunctionSymbol(soft.Compare));

            XtensaLabel yes = NewLabel("dyes");
            XtensaLabel done = NewLabel("ddone");

            switch (comparison)
            {
                case IRComparisonOperator.EqualTo:
                    this.Assembler.BranchZero(true, A2, yes);
                    break;

                case IRComparisonOperator.NotEqualTo:
                    this.Assembler.BranchZero(false, A2, yes);
                    break;

                case IRComparisonOperator.LessThan:
                    this.Assembler.Movi(A6, -1);
                    this.Assembler.Branch(XtensaBranch.Equal, A2, A6, yes);
                    break;

                case IRComparisonOperator.LessThanOrEqual:
                    this.Assembler.Movi(A6, -1);
                    this.Assembler.Branch(XtensaBranch.Equal, A2, A6, yes);
                    this.Assembler.BranchZero(true, A2, yes);
                    break;

                case IRComparisonOperator.GreaterThan:
                    this.Assembler.Movi(A6, 1);
                    this.Assembler.Branch(XtensaBranch.Equal, A2, A6, yes);
                    break;

                case IRComparisonOperator.GreaterThanOrEqual:
                    this.Assembler.Movi(A6, 1);
                    this.Assembler.Branch(XtensaBranch.Equal, A2, A6, yes);
                    this.Assembler.BranchZero(true, A2, yes);
                    break;
            }

            this.Assembler.Movi(A2, 0);
            this.Assembler.Jump(done);
            this.Assembler.Bind(yes);
            this.Assembler.Movi(A2, 1);
            this.Assembler.Bind(done);
        }

        XtensaSoftFloat SoftFloat(string what) => this.Context.SoftFloat ?? throw ErrorHandler.Create($"{what} is not supported on the ESP32 yet.");

        void EmitStore(IRStore store)
        {
            if (store.Offset == null)
            {
                switch (store.Target)
                {
                    case IRLocal local:
                        EmitValue(store.Value);
                        Coerce(store.Value.ValueType, local.ValueType);
                        StoreSlot(SlotOf(local), XtensaLayout.Classify(local.ValueType));
                        return;

                    case IRGlobal global:
                        EmitValue(store.Value);
                        Coerce(store.Value.ValueType, global.ValueType);
                        this.Assembler.LoadAddress(A8, this.Context.GlobalSymbol(global));
                        this.Assembler.S32i(A2, A8, 0);

                        if (XtensaLayout.Words(global.ValueType) == 2)
                        {
                            this.Assembler.S32i(A3, A8, 4);
                        }
                        return;
                }

                EmitValue(store.Target);
                Temporary address = SaveTemporary(XtensaValueClass.Word);

                EmitValue(store.Value);
                LoadTemporary(address, A6, A7);
                Release(address);

                StoreChecked(store.Value.ValueType, A6, 0);
                return;
            }

            int displacement = 0;
            bool nullCheck = false;
            bool aligned = false;
            IRType type = store.Value.ValueType;
            IRValueInstruction? offsetValue = null;

            switch (store.Offset)
            {
                case IRStructProperty property:
                    displacement = XtensaLayout.OffsetOf(property);
                    nullCheck = true;
                    aligned = true;
                    type = property.ValueType;
                    break;

                case IRConstantInteger constant:
                    displacement = (int)constant.Value;
                    break;

                default:
                    offsetValue = store.Offset;
                    break;
            }

            EmitValue(store.Target);
            Temporary pointer = SaveTemporary(XtensaValueClass.Word);
            Temporary? offset = null;

            if (offsetValue != null)
            {
                EmitValue(offsetValue);
                offset = SaveTemporary(XtensaValueClass.Word);
            }

            EmitValue(store.Value);
            Coerce(store.Value.ValueType, type);

            LoadTemporary(pointer, A6, A7);

            if (offset != null)
            {
                LoadTemporary(offset, A7, A8);
                this.Assembler.Add(A6, A6, A7);
            }

            Release(offset);
            Release(pointer);

            if (nullCheck)
            {
                this.Assembler.BranchZero(true, A6, PanicStub(NullReferenceMessage));
            }

            if (aligned)
            {
                StoreTyped(type, A6, displacement);
            }
            else
            {
                StoreChecked(type, A6, displacement);
            }
        }

        void StoreChecked(IRType type, XtensaRegister address, int displacement)
        {
            int size = XtensaLayout.SizeOf(type);

            if (size == 1 || XtensaLayout.Classify(type) == XtensaValueClass.Fat)
            {
                StoreTyped(type, address, displacement);
                return;
            }

            if (displacement != 0)
            {
                this.Assembler.AddConstant(A6, address, displacement, A9);
                address = A6;
            }

            XtensaLabel slow = NewLabel("unaligned");
            XtensaLabel done = NewLabel("stored");

            this.Assembler.Extui(A7, address, 0, size == 2 ? 1 : 2);
            this.Assembler.BranchZero(false, A7, slow);
            StoreTyped(type, address, 0);
            this.Assembler.Jump(done);

            this.Assembler.Bind(slow);

            for (int i = 0; i < Math.Min(size, 4); i++)
            {
                this.Assembler.Srli(A7, A2, 8 * i);
                this.Assembler.Store(1, A7, address, i);
            }

            for (int i = 4; i < size; i++)
            {
                this.Assembler.Srli(A7, A3, 8 * (i - 4));
                this.Assembler.Store(1, A7, address, i);
            }

            this.Assembler.Bind(done);
        }

        void LoadChecked(IRType type, XtensaRegister address, int displacement)
        {
            int size = XtensaLayout.SizeOf(type);

            if (size == 1 || XtensaLayout.Classify(type) == XtensaValueClass.Fat)
            {
                LoadTyped(type, address, displacement);
                return;
            }

            if (displacement != 0)
            {
                this.Assembler.AddConstant(A6, address, displacement, A9);
                address = A6;
            }
            else if (address != A6)
            {
                this.Assembler.Mov(A6, address);
                address = A6;
            }

            XtensaLabel slow = NewLabel("unaligned");
            XtensaLabel done = NewLabel("loaded");

            this.Assembler.Extui(A7, address, 0, size == 2 ? 1 : 2);
            this.Assembler.BranchZero(false, A7, slow);
            LoadTyped(type, address, 0);
            this.Assembler.Jump(done);

            this.Assembler.Bind(slow);
            this.Assembler.Movi(A2, 0);
            this.Assembler.Movi(A3, 0);

            for (int i = 0; i < size; i++)
            {
                XtensaRegister target = i < 4 ? A2 : A3;
                this.Assembler.Load(1, false, A7, address, i);

                if (i % 4 != 0)
                {
                    this.Assembler.Slli(A7, A7, 8 * (i % 4));
                }

                this.Assembler.Or(target, target, A7);
            }

            if (size == 2 && XtensaLayout.IsSigned(type))
            {
                this.Assembler.Sext(A2, A2, 15);
            }

            this.Assembler.Bind(done);
        }

        ArraySource SourceOf(IRValueInstruction target)
        {
            if (target is IRLocal local && !this.ResultSlots.ContainsKey(target))
            {
                return new ArraySource { Slot = SlotOf(local) };
            }

            EmitValue(target);
            return new ArraySource { Temporary = SaveTemporary(XtensaValueClass.Fat) };
        }

        void LoadArray(ArraySource source)
        {
            if (source.Temporary != null)
            {
                LoadTemporary(source.Temporary, A6, A7);
            }
            else
            {
                LoadSlot(source.Slot, XtensaValueClass.Fat, A6, A7);
            }
        }

        void ElementAddress(IRType indexType, int width, bool check)
        {
            if (check)
            {
                if (XtensaLayout.Classify(indexType) == XtensaValueClass.Pair)
                {
                    this.Assembler.BranchZero(false, A5, PanicStub(IndexOutOfRangeMessage));
                }

                this.Assembler.Branch(XtensaBranch.GreaterOrEqualUnsigned, A4, A7, PanicStub(IndexOutOfRangeMessage));
            }

            switch (width)
            {
                case 1:
                    this.Assembler.Add(A6, A4, A6);
                    break;

                case 2:
                    this.Assembler.AddShifted(1, A6, A4, A6);
                    break;

                case 4:
                    this.Assembler.AddShifted(2, A6, A4, A6);
                    break;

                case 8:
                    this.Assembler.AddShifted(3, A6, A4, A6);
                    break;

                default:
                    this.Assembler.LoadConstant(A7, width);
                    this.Assembler.Mull(A7, A4, A7);
                    this.Assembler.Add(A6, A6, A7);
                    break;
            }
        }

        void EmitStoreElement(IRStoreElement store)
        {
            int width = XtensaLayout.ElementSize(store.ElementType);

            ArraySource array = SourceOf(store.Target);
            Temporary? index = null;

            if (!IsSimple(store.Index))
            {
                EmitValue(store.Index);
                index = SaveTemporary(XtensaLayout.Classify(store.Index.ValueType));
            }

            EmitValue(store.Value);

            if (!IRStructSchema.IsInline(store.ElementType))
            {
                Coerce(store.Value.ValueType, store.ElementType);
            }

            if (index != null)
            {
                LoadTemporary(index, A4, A5);
            }
            else
            {
                LoadSimple(store.Index, A4, A5);
            }

            LoadArray(array);
            ElementAddress(store.Index.ValueType, width, check: true);

            Release(index);
            Release(array.Temporary);

            if (IRStructSchema.IsInline(store.ElementType))
            {
                CopyStruct((IRStructSchema)store.ElementType.UserObject!, A6, 0);
            }
            else
            {
                StoreTyped(store.ElementType, A6, 0);
            }
        }

        void CopyStruct(IRStructSchema schema, XtensaRegister destination, int offset)
        {
            this.Assembler.BranchZero(true, A2, PanicStub(NullReferenceMessage));

            if (offset != 0)
            {
                this.Assembler.AddConstant(A7, destination, offset, A9);
                destination = A7;
            }

            for (int i = 0; i < XtensaLayout.SizeOf(schema); i += 4)
            {
                (XtensaRegister sb, int sd) = Fit(A2, i, 4, 0);
                this.Assembler.L32i(A8, sb, sd);
                (XtensaRegister db, int dd) = Fit(destination, i, 4, 0);
                this.Assembler.S32i(A8, db, dd);
            }
        }

        #endregion

        #region Values

        void EmitValue(IRValueInstruction value)
        {
            if (this.ResultSlots.TryGetValue(value, out int slot))
            {
                LoadSlot(slot, XtensaLayout.Classify(value.ValueType), A2, A3);
                return;
            }

            switch (value)
            {
                case IRConstantInteger or IRConstantString or IRConstantBytes or IRLocal or IRGlobal or IRStructProperty:
                    LoadSimple(value, A2, A3);
                    break;

                case IRFunctionReference functionReference:
                    this.Assembler.LoadAddress(A2, this.Context.FunctionSymbol(functionReference.Function));
                    break;

                case IRAdd add:
                    EmitArithmetic(ArithmeticOperation.Add, add.Left, add.Right, add.ValueType);
                    break;

                case IRSubtract subtract:
                    EmitArithmetic(ArithmeticOperation.Subtract, subtract.Left, subtract.Right, subtract.ValueType);
                    break;

                case IRMultiply multiply:
                    EmitArithmetic(ArithmeticOperation.Multiply, multiply.Left, multiply.Right, multiply.ValueType);
                    break;

                case IRDivide divide:
                    EmitArithmetic(ArithmeticOperation.Divide, divide.Left, divide.Right, divide.ValueType);
                    break;

                case IRMath math:
                    EmitMath(math);
                    break;

                case IRBitwise bitwise:
                    EmitBitwise(bitwise);
                    break;

                case IRCompare compare:
                    {
                        XtensaLabel yes = NewLabel("true");
                        XtensaLabel done = NewLabel("compared");

                        EmitCompareBranch(compare, true, yes);
                        this.Assembler.Movi(A2, 0);
                        this.Assembler.Jump(done);
                        this.Assembler.Bind(yes);
                        this.Assembler.Movi(A2, 1);
                        this.Assembler.Bind(done);
                    }
                    break;

                case IRConvert convert:
                    EmitConvert(convert);
                    break;

                case IRLoad load:
                    EmitLoad(load);
                    break;

                case IRLoadElement loadElement:
                    EmitLoadElement(loadElement);
                    break;

                case IRLength length:
                    if (length.Target is IRLocal lengthLocal && !this.ResultSlots.ContainsKey(lengthLocal))
                    {
                        (XtensaRegister b, int d) = FrameAddress(SlotOffset(SlotOf(lengthLocal)) + 4, 4);
                        this.Assembler.L32i(A2, b, d);
                    }
                    else
                    {
                        EmitValue(length.Target);
                        this.Assembler.Mov(A2, A3);
                    }
                    break;

                case IRFatPointer fatPointer:
                    {
                        EmitValue(fatPointer.Length);
                        Temporary length = SaveTemporary(XtensaValueClass.Word);

                        EmitValue(fatPointer.Pointer);
                        LoadTemporary(length, A3, A4);
                        Release(length);
                    }
                    break;

                case IRCall or IRCallIndirect:
                    EmitInvocation(value);
                    break;

                case IRInstantiateArray instantiateArray:
                    EmitInstantiateArray(instantiateArray);
                    break;

                case IRInstantiateStruct instantiateStruct:
                    EmitInstantiateStruct(instantiateStruct);
                    break;

                case IRMalloc malloc:
                    EmitValue(malloc.Bytes);
                    this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Malloc));
                    break;

                case IRSizeOf sizeOf:
                    this.Assembler.LoadConstant(A2, sizeOf.Target switch
                    {
                        IRStructSchema schema => XtensaLayout.SizeOf(schema),
                        IRType type => XtensaLayout.SizeOf(type),
                        IRValueInstruction instruction => XtensaLayout.SizeOf(instruction.ValueType),
                        _ => throw ErrorHandler.Create("Cannot take the size of this value."),
                    });
                    break;

                case IRDispatch or IRAwait:
                    throw ErrorHandler.Create("Parallel functions (GPU kernels) are not supported on the ESP32.");

                default:
                    throw ErrorHandler.CreateNotImplimented($"No Xtensa translation implemented for instruction '{value.GetType().Name}'");
            }
        }

        enum ArithmeticOperation
        {
            Add,
            Subtract,
            Multiply,
            Divide,
        }

        void EmitArithmetic(ArithmeticOperation operation, IRValueInstruction left, IRValueInstruction right, IRType type)
        {
            XtensaValueClass valueClass = XtensaLayout.Classify(type);

            switch (valueClass)
            {
                case XtensaValueClass.Word:
                    EmitOperands(left, right);
                    EmitWordArithmetic(operation, type);
                    Normalize(type);
                    return;

                case XtensaValueClass.Pair:
                    EmitOperands(left, right);
                    EmitPairArithmetic(operation, type);
                    return;

                case XtensaValueClass.Single:
                    EmitOperands(left, right);
                    EmitSingleArithmetic(operation);
                    return;

                case XtensaValueClass.Double:
                    {
                        XtensaSoftFloat soft = SoftFloat("Arithmetic on f64 values");
                        EmitOperands(left, right);

                        IRFunction function = operation switch
                        {
                            ArithmeticOperation.Add => soft.Add,
                            ArithmeticOperation.Subtract => soft.Subtract,
                            ArithmeticOperation.Multiply => soft.Multiply,
                            _ => soft.Divide,
                        };

                        this.Assembler.Call0(this.Context.FunctionSymbol(function));
                    }
                    return;
            }

            throw ErrorHandler.Create($"Arithmetic is not supported on values of type '{type.Dump(0)}'.");
        }

        void EmitWordArithmetic(ArithmeticOperation operation, IRType type)
        {
            switch (operation)
            {
                case ArithmeticOperation.Add:
                    this.Assembler.Add(A2, A2, A4);
                    break;

                case ArithmeticOperation.Subtract:
                    this.Assembler.Sub(A2, A2, A4);
                    break;

                case ArithmeticOperation.Multiply:
                    this.Assembler.Mull(A2, A2, A4);
                    break;

                case ArithmeticOperation.Divide:
                    this.Assembler.BranchZero(true, A4, PanicStub(DivideByZeroMessage));

                    if (XtensaLayout.IsSigned(type))
                    {
                        XtensaLabel divide = NewLabel("divide");
                        XtensaLabel done = NewLabel("divided");

                        this.Assembler.Movi(A6, -1);
                        this.Assembler.Branch(XtensaBranch.NotEqual, A4, A6, divide);
                        this.Assembler.Neg(A2, A2);
                        this.Assembler.Jump(done);
                        this.Assembler.Bind(divide);
                        this.Assembler.Quos(A2, A2, A4);
                        this.Assembler.Bind(done);
                    }
                    else
                    {
                        this.Assembler.Quou(A2, A2, A4);
                    }
                    break;
            }
        }

        void EmitPairArithmetic(ArithmeticOperation operation, IRType type)
        {
            switch (operation)
            {
                case ArithmeticOperation.Add:
                    {
                        XtensaLabel noCarry = NewLabel("nocarry");
                        this.Assembler.Add(A2, A2, A4);
                        this.Assembler.Add(A3, A3, A5);
                        this.Assembler.Branch(XtensaBranch.GreaterOrEqualUnsigned, A2, A4, noCarry);
                        this.Assembler.Addi(A3, A3, 1);
                        this.Assembler.Bind(noCarry);
                    }
                    break;

                case ArithmeticOperation.Subtract:
                    {
                        XtensaLabel noBorrow = NewLabel("noborrow");
                        this.Assembler.Branch(XtensaBranch.GreaterOrEqualUnsigned, A2, A4, noBorrow);
                        this.Assembler.Addi(A3, A3, -1);
                        this.Assembler.Bind(noBorrow);
                        this.Assembler.Sub(A2, A2, A4);
                        this.Assembler.Sub(A3, A3, A5);
                    }
                    break;

                case ArithmeticOperation.Multiply:
                    this.Assembler.Mull(A6, A2, A5);
                    this.Assembler.Mull(A7, A3, A4);
                    this.Assembler.Add(A6, A6, A7);
                    this.Assembler.Muluh(A7, A2, A4);
                    this.Assembler.Add(A3, A6, A7);
                    this.Assembler.Mull(A2, A2, A4);
                    break;

                case ArithmeticOperation.Divide:
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(type) ? this.Context.Divide64 : this.Context.DivideUnsigned64));
                    break;
            }
        }

        static readonly int[] DivideSequence =
        [
            0xfa3270, 0xfa42b0, 0xfa5130, 0x6a5430, 0xfa6300, 0xfa7200, 0xfa21b0, 0x6a6560,
            0xfa5130, 0xfa0030, 0xfa8260, 0x6a5460, 0x6a0830, 0xfa71d0, 0x6a6560, 0x6a8400,
            0xfa3130, 0x6a3460, 0x6a0860, 0xfa2260, 0x6a6360, 0x6a2400, 0xfa07f0, 0xfa67e0,
            0x7a0260,
        ];

        void EmitSingleArithmetic(ArithmeticOperation operation)
        {
            if (operation == ArithmeticOperation.Divide)
            {
                this.Assembler.Wfr(1, A2);
                this.Assembler.Wfr(2, A4);

                foreach (int word in DivideSequence)
                {
                    this.Assembler.Raw24(word);
                }

                this.Assembler.Rfr(A2, 0);
                return;
            }

            this.Assembler.Wfr(0, A2);
            this.Assembler.Wfr(1, A4);
            this.Assembler.FloatArithmetic(operation switch
            {
                ArithmeticOperation.Add => 0,
                ArithmeticOperation.Subtract => 1,
                _ => 2,
            }, 0, 0, 1);
            this.Assembler.Rfr(A2, 0);
        }

        void EmitMath(IRMath math)
        {
            XtensaValueClass valueClass = XtensaLayout.Classify(math.ValueType);

            if (math.Operation == IRMathOperation.Reinterpret)
            {
                EmitValue(math.Value);
                return;
            }

            switch (math.Operation, valueClass)
            {
                case (IRMathOperation.SquareRoot, XtensaValueClass.Single):
                    EmitValue(math.Value);
                    this.Assembler.Call0(this.Context.FunctionSymbol(SoftFloat("Square roots").SquareRootSingle));
                    return;

                case (IRMathOperation.SquareRoot, XtensaValueClass.Double):
                    EmitValue(math.Value);
                    this.Assembler.Call0(this.Context.FunctionSymbol(SoftFloat("Square roots").SquareRoot));
                    return;
            }

            throw ErrorHandler.Create($"'{math.Operation}' is not supported on values of type '{math.ValueType.Dump(0)}'.");
        }

        void EmitBitwise(IRBitwise bitwise)
        {
            XtensaValueClass valueClass = XtensaLayout.Classify(bitwise.ValueType);
            bool shift = bitwise.Operator is IRBitwiseOperator.ShiftLeft or IRBitwiseOperator.ShiftRight;

            if (valueClass is not (XtensaValueClass.Word or XtensaValueClass.Pair))
            {
                throw ErrorHandler.Create($"Bitwise operations are not supported on values of type '{bitwise.ValueType.Dump(0)}'.");
            }

            if (shift && valueClass == XtensaValueClass.Word && bitwise.Right is IRConstantInteger count && !this.ResultSlots.ContainsKey(count))
            {
                int amount = (int)(XtensaLayout.Bits(count.IntegerType, count.Value) & 31);
                EmitValue(bitwise.Left);

                if (bitwise.Operator == IRBitwiseOperator.ShiftLeft)
                {
                    this.Assembler.Slli(A2, A2, amount);
                }
                else if (XtensaLayout.IsSigned(bitwise.ValueType))
                {
                    this.Assembler.Srai(A2, A2, amount);
                }
                else
                {
                    this.Assembler.Srli(A2, A2, amount);
                }

                Normalize(bitwise.ValueType);
                return;
            }

            EmitOperands(bitwise.Left, bitwise.Right, convertRight: !shift);

            if (!shift)
            {
                switch (bitwise.Operator)
                {
                    case IRBitwiseOperator.And:
                        this.Assembler.And(A2, A2, A4);
                        if (valueClass == XtensaValueClass.Pair) this.Assembler.And(A3, A3, A5);
                        break;

                    case IRBitwiseOperator.Or:
                        this.Assembler.Or(A2, A2, A4);
                        if (valueClass == XtensaValueClass.Pair) this.Assembler.Or(A3, A3, A5);
                        break;

                    case IRBitwiseOperator.Xor:
                        this.Assembler.Xor(A2, A2, A4);
                        if (valueClass == XtensaValueClass.Pair) this.Assembler.Xor(A3, A3, A5);
                        break;
                }

                return;
            }

            bool signed = XtensaLayout.IsSigned(bitwise.ValueType);

            if (valueClass == XtensaValueClass.Word)
            {
                if (bitwise.Operator == IRBitwiseOperator.ShiftLeft)
                {
                    this.Assembler.Ssl(A4);
                    this.Assembler.Sll(A2, A2);
                }
                else
                {
                    this.Assembler.Ssr(A4);

                    if (signed)
                    {
                        this.Assembler.Sra(A2, A2);
                    }
                    else
                    {
                        this.Assembler.Srl(A2, A2);
                    }
                }

                Normalize(bitwise.ValueType);
                return;
            }

            XtensaLabel big = NewLabel("shift64");
            XtensaLabel done = NewLabel("shifted");

            this.Assembler.BranchBit(true, A4, 5, big);

            if (bitwise.Operator == IRBitwiseOperator.ShiftLeft)
            {
                this.Assembler.Ssl(A4);
                this.Assembler.Src(A3, A3, A2);
                this.Assembler.Sll(A2, A2);
                this.Assembler.Jump(done);

                this.Assembler.Bind(big);
                this.Assembler.Ssl(A4);
                this.Assembler.Sll(A3, A2);
                this.Assembler.Movi(A2, 0);
            }
            else
            {
                this.Assembler.Ssr(A4);
                this.Assembler.Src(A2, A3, A2);

                if (signed)
                {
                    this.Assembler.Sra(A3, A3);
                }
                else
                {
                    this.Assembler.Srl(A3, A3);
                }

                this.Assembler.Jump(done);

                this.Assembler.Bind(big);
                this.Assembler.Ssr(A4);

                if (signed)
                {
                    this.Assembler.Sra(A2, A3);
                    this.Assembler.Srai(A3, A3, 31);
                }
                else
                {
                    this.Assembler.Srl(A2, A3);
                    this.Assembler.Movi(A3, 0);
                }
            }

            this.Assembler.Bind(done);
        }

        void EmitConvert(IRConvert convert)
        {
            IRType source = convert.Value.ValueType;
            IRType target = convert.ValueType;

            XtensaValueClass from = XtensaLayout.Classify(source);
            XtensaValueClass to = XtensaLayout.Classify(target);

            EmitValue(convert.Value);

            if (target.DataType == IRDataTypes.Bool && from != XtensaValueClass.Void)
            {
                XtensaLabel done = NewLabel("bool");

                switch (from)
                {
                    case XtensaValueClass.Pair:
                    case XtensaValueClass.Fat:
                        this.Assembler.Or(A2, A2, A3);
                        break;

                    case XtensaValueClass.Single:
                        this.Assembler.Slli(A2, A2, 1);
                        break;

                    case XtensaValueClass.Double:
                        this.Assembler.Slli(A3, A3, 1);
                        this.Assembler.Or(A2, A2, A3);
                        break;
                }

                this.Assembler.BranchZero(true, A2, done);
                this.Assembler.Movi(A2, 1);
                this.Assembler.Bind(done);
                return;
            }

            switch (from, to)
            {
                case (XtensaValueClass.Word, XtensaValueClass.Word):
                    Normalize(target);
                    return;

                case (XtensaValueClass.Word, XtensaValueClass.Pair):
                    Extend(XtensaLayout.IsSigned(source));
                    return;

                case (XtensaValueClass.Pair, XtensaValueClass.Word):
                    Normalize(target);
                    return;

                case (XtensaValueClass.Pair, XtensaValueClass.Pair):
                case (XtensaValueClass.Single, XtensaValueClass.Single):
                case (XtensaValueClass.Double, XtensaValueClass.Double):
                case (XtensaValueClass.Fat, XtensaValueClass.Fat):
                case (XtensaValueClass.Fat, XtensaValueClass.Word):
                    return;

                case (XtensaValueClass.Fat, XtensaValueClass.Pair):
                    this.Assembler.Movi(A3, 0);
                    return;

                case (XtensaValueClass.Word, XtensaValueClass.Single):
                    this.Assembler.FloatFromInteger(!XtensaLayout.IsSigned(source), 0, A2);
                    this.Assembler.Rfr(A2, 0);
                    return;

                case (XtensaValueClass.Single, XtensaValueClass.Word):
                    this.Assembler.Wfr(0, A2);
                    this.Assembler.IntegerFromFloat(target.DataType == IRDataTypes.UInt32, A2, 0);
                    Normalize(target);
                    return;
            }

            XtensaSoftFloat soft = SoftFloat($"Converting {source.DataType} to {target.DataType}");

            switch (from, to)
            {
                case (XtensaValueClass.Word, XtensaValueClass.Double):
                    Extend(XtensaLayout.IsSigned(source));
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(source) ? soft.FromInt64 : soft.FromUInt64));
                    return;

                case (XtensaValueClass.Pair, XtensaValueClass.Double):
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(source) ? soft.FromInt64 : soft.FromUInt64));
                    return;

                case (XtensaValueClass.Double, XtensaValueClass.Word):
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(target) || target.DataType is not (IRDataTypes.UInt32) ? soft.ToInt64 : soft.ToUInt64));
                    Normalize(target);
                    return;

                case (XtensaValueClass.Double, XtensaValueClass.Pair):
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(target) ? soft.ToInt64 : soft.ToUInt64));
                    return;

                case (XtensaValueClass.Single, XtensaValueClass.Double):
                    this.Assembler.Call0(this.Context.FunctionSymbol(soft.FromSingle));
                    return;

                case (XtensaValueClass.Double, XtensaValueClass.Single):
                    this.Assembler.Call0(this.Context.FunctionSymbol(soft.ToSingle));
                    return;

                case (XtensaValueClass.Pair, XtensaValueClass.Single):
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(source) ? soft.FromInt64 : soft.FromUInt64));
                    this.Assembler.Call0(this.Context.FunctionSymbol(soft.ToSingle));
                    return;

                case (XtensaValueClass.Single, XtensaValueClass.Pair):
                    this.Assembler.Call0(this.Context.FunctionSymbol(soft.FromSingle));
                    this.Assembler.Call0(this.Context.FunctionSymbol(XtensaLayout.IsSigned(target) ? soft.ToInt64 : soft.ToUInt64));
                    return;
            }

            throw ErrorHandler.CreateNotImplimented($"Converting '{source.Dump(0)}' to '{target.Dump(0)}' is not supported when targeting Xtensa.");
        }

        void EmitLoad(IRLoad load)
        {
            if (load.Offset == null)
            {
                switch (load.Target)
                {
                    case IRLocal or IRGlobal:
                        LoadSimple(load.Target, A2, A3);
                        Coerce(load.Target.ValueType, load.ValueType);
                        return;
                }

                EmitValue(load.Target);
                LoadChecked(load.ValueType, A2, 0);
                return;
            }

            switch (load.Offset)
            {
                case IRStructProperty property:
                    EmitValue(load.Target);
                    this.Assembler.BranchZero(true, A2, PanicStub(NullReferenceMessage));
                    LoadTyped(load.ValueType, A2, XtensaLayout.OffsetOf(property));
                    return;

                case IRConstantInteger constant:
                    EmitValue(load.Target);
                    LoadChecked(load.ValueType, A2, (int)constant.Value);
                    return;
            }

            EmitOperands(load.Target, load.Offset, convertRight: false);
            this.Assembler.Add(A2, A2, A4);
            LoadChecked(load.ValueType, A2, 0);
        }

        void EmitLoadElement(IRLoadElement load)
        {
            int width = XtensaLayout.ElementSize(load.ElementType);

            ArraySource array = SourceOf(load.Target);

            EmitValue(load.Index);
            this.Assembler.Mov(A4, A2);
            this.Assembler.Mov(A5, A3);

            LoadArray(array);
            ElementAddress(load.Index.ValueType, width, check: true);
            Release(array.Temporary);

            if (IRStructSchema.IsInline(load.ElementType))
            {
                this.Assembler.Mov(A2, A6);
            }
            else
            {
                LoadTyped(load.ElementType, A6, 0);
            }
        }

        void EmitInstantiateStruct(IRInstantiateStruct instantiate)
        {
            this.Assembler.LoadConstant(A2, XtensaLayout.SizeOf(instantiate.StructSchema));
            this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Malloc));

            if (instantiate.PropertyValues.Count == 0)
            {
                return;
            }

            Temporary pointer = SaveTemporary(XtensaValueClass.Word);

            for (int i = 0; i < instantiate.PropertyValues.Count; i++)
            {
                IRStructProperty property = instantiate.StructSchema.Properties[i];
                IRValueInstruction propertyValue = instantiate.PropertyValues[i];

                EmitValue(propertyValue);
                Coerce(propertyValue.ValueType, property.ValueType);
                LoadTemporary(pointer, A6, A7);
                StoreTyped(property.ValueType, A6, XtensaLayout.OffsetOf(property));
            }

            LoadTemporary(pointer);
            Release(pointer);
        }

        void EmitInstantiateArray(IRInstantiateArray instantiate)
        {
            IRType elementType = instantiate.ArraySchema.ElementType;
            int width = XtensaLayout.ElementSize(elementType);

            EmitValue(instantiate.Size);

            if (XtensaLayout.Classify(instantiate.Size.ValueType) == XtensaValueClass.Pair)
            {
                this.Assembler.BranchZero(false, A3, PanicStub(NegativeSizeMessage));
            }

            this.Assembler.BranchNegative(true, A2, PanicStub(NegativeSizeMessage));

            Temporary length = SaveTemporary(XtensaValueClass.Word);

            if (width != 1)
            {
                this.Assembler.LoadConstant(A4, width);
                this.Assembler.Mull(A2, A2, A4);
            }

            this.Assembler.Call0(this.Context.FunctionSymbol(this.Context.Malloc));

            Temporary pointer = SaveTemporary(XtensaValueClass.Word);

            for (int i = 0; i < instantiate.ElementValues.Count; i++)
            {
                IRValueInstruction element = instantiate.ElementValues[i];
                EmitValue(element);

                LoadTemporary(pointer, A6, A7);

                if (IRStructSchema.IsInline(elementType))
                {
                    CopyStruct((IRStructSchema)elementType.UserObject!, A6, i * width);
                }
                else
                {
                    Coerce(element.ValueType, elementType);
                    StoreTyped(elementType, A6, i * width);
                }
            }

            LoadTemporary(pointer, A2, A4);
            LoadTemporary(length, A3, A4);

            Release(pointer);
            Release(length);
        }

        void EmitInvocation(IRValueInstruction invocation)
        {
            switch (invocation)
            {
                case IRCall call when call.Function is IRFunctionImport functionImport && this.Context.Intrinsics.TryGetValue(functionImport, out XtensaIntrinsic intrinsic):
                    EmitIntrinsic(intrinsic, call);
                    break;

                case IRCall call when call.Function is IRFunctionImport functionImport && this.Context.RuntimeImports.TryGetValue(functionImport, out IRFunction? runtimeFunction):
                    EmitCall(call.Arguments, runtimeFunction.Parameters.Select(p => p.ValueType).ToList(), null, this.Context.FunctionSymbol(runtimeFunction));
                    break;

                case IRCall call when call.Function is IRFunctionImport functionImport:
                    throw ErrorHandler.Create($"'{functionImport.ModuleName}'.{functionImport.Name} is not available on the ESP32.");

                case IRCall call:
                    EmitCall(call.Arguments, call.Function.Parameters.Select(p => p.ValueType).ToList(), null, this.Context.FunctionSymbol(call.Function));
                    break;

                case IRCallIndirect indirect:
                    EmitCall(indirect.Arguments, indirect.Arguments.Select(a => a.ValueType).ToList(), indirect.FunctionPointer, null);
                    break;

                default:
                    throw ErrorHandler.Create($"'{invocation.GetType().Name}' is not an invocation.");
            }
        }

        void EmitIntrinsic(XtensaIntrinsic intrinsic, IRCall call)
        {
            switch (intrinsic)
            {
                case XtensaIntrinsic.Read32:
                    EmitValue(call.Arguments[0]);
                    this.Assembler.Memw();
                    this.Assembler.L32i(A2, A2, 0);
                    break;

                case XtensaIntrinsic.Read8:
                    EmitValue(call.Arguments[0]);
                    this.Assembler.Memw();
                    this.Assembler.Load(1, false, A2, A2, 0);
                    break;

                case XtensaIntrinsic.Write32:
                case XtensaIntrinsic.Write8:
                    EmitOperands(call.Arguments[0], call.Arguments[1], convertRight: false);
                    this.Assembler.Memw();
                    this.Assembler.Store(intrinsic == XtensaIntrinsic.Write32 ? 4 : 1, A4, A2, 0);
                    break;

                case XtensaIntrinsic.Cycles:
                    this.Assembler.Rsr(A2, XtensaSpecialRegister.CCOUNT);
                    break;

                case XtensaIntrinsic.Address:
                    EmitValue(call.Arguments[0]);
                    break;

                case XtensaIntrinsic.Barrier:
                    this.Assembler.Memw();
                    break;
            }
        }

        bool IsDeferred(IRValueInstruction argument) => IsSimple(argument);

        void EmitCall(List<IRValueInstruction> arguments, List<IRType> parameterTypes, IRValueInstruction? functionPointer, XtensaSymbol? target)
        {
            List<Temporary> temporaries = new List<Temporary>();
            Temporary? pointer = null;
            List<(IRValueInstruction Value, Temporary? Temporary, IRType Type)> sources = new();

            if (functionPointer != null)
            {
                EmitValue(functionPointer);
                pointer = SaveTemporary(XtensaValueClass.Word);
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                IRValueInstruction argument = arguments[i];
                IRType type = i < parameterTypes.Count ? parameterTypes[i] : argument.ValueType;
                Temporary? temporary = null;

                if (!IsDeferred(argument) || XtensaLayout.Classify(argument.ValueType) != XtensaLayout.Classify(type))
                {
                    EmitValue(argument);
                    Coerce(argument.ValueType, type);
                    temporary = SaveTemporary(XtensaLayout.Classify(type));
                    temporaries.Add(temporary);
                }

                sources.Add((argument, temporary, type));
            }

            List<(int Source, int Part)> words = new List<(int Source, int Part)>();

            for (int i = 0; i < sources.Count; i++)
            {
                for (int part = 0; part < XtensaLayout.Words(sources[i].Type); part++)
                {
                    words.Add((i, part));
                }
            }

            for (int word = ArgumentRegisters; word < words.Count; word++)
            {
                (int source, int part) = words[word];
                LoadSource(sources[source], A2, A3);
                (XtensaRegister b, int d) = FrameAddress(4 * (word - ArgumentRegisters), 4);
                this.Assembler.S32i(part == 0 ? A2 : A3, b, d);
            }

            this.MaxOutgoing = Math.Max(this.MaxOutgoing, 4 * Math.Max(0, words.Count - ArgumentRegisters));

            for (int i = 0; i < sources.Count; i++)
            {
                int first = words.FindIndex(w => w.Source == i);

                if (first < 0 || first >= ArgumentRegisters)
                {
                    continue;
                }

                XtensaRegister low = (XtensaRegister)((int)A2 + first);
                XtensaRegister high = first + 1 < ArgumentRegisters ? (XtensaRegister)((int)A2 + first + 1) : A9;

                if (high == A9 && XtensaLayout.Words(sources[i].Type) == 2)
                {
                    LoadSource(sources[i], low, A8);
                    continue;
                }

                LoadSource(sources[i], low, high);
            }

            if (pointer != null)
            {
                LoadTemporary(pointer, A8, A9);
                this.Assembler.Callx0Aligned(A8);
            }
            else
            {
                this.Assembler.Call0(target!);
            }

            for (int i = temporaries.Count - 1; i >= 0; i--)
            {
                Release(temporaries[i]);
            }

            Release(pointer);
        }

        void LoadSource((IRValueInstruction Value, Temporary? Temporary, IRType Type) source, XtensaRegister low, XtensaRegister high)
        {
            if (source.Temporary != null)
            {
                LoadTemporary(source.Temporary, low, high);
                return;
            }

            LoadSimple(source.Value, low, high);
        }

        #endregion
    }
}
