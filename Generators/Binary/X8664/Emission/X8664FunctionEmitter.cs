using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Model;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal class X8664FunctionEmitter
    {
        public const string DivideByZeroMessage = "Attempted to divide by zero.";
        public const string IndexOutOfRangeMessage = "Index was outside the bounds of the array.";
        public const string NullReferenceMessage = "Object reference not set to an instance of an object.";
        public const string NegativeSizeMessage = "Arrays cannot have a negative size.";

        static readonly X8664Register[] IntegerArgumentRegisters = [X8664Register.RCX, X8664Register.RDX, X8664Register.R8, X8664Register.R9];

        static readonly X8664XmmRegister[] FloatArgumentRegisters = [X8664XmmRegister.XMM0, X8664XmmRegister.XMM1, X8664XmmRegister.XMM2, X8664XmmRegister.XMM3];

        X8664Context Context { get; set; }

        X8664Assembler Assembler => this.Context.Assembler;

        IRFunction Function { get; set; }

        Dictionary<IRLocal, int> LocalSlots { get; set; } = new Dictionary<IRLocal, int>();

        Dictionary<IRInstruction, int> ResultSlots { get; set; } = new Dictionary<IRInstruction, int>();

        Stack<int> FreeTemporaries { get; set; } = new Stack<int>();

        Stack<(X8664Symbol Continue, X8664Symbol Exit)> Loops { get; set; } = new Stack<(X8664Symbol Continue, X8664Symbol Exit)>();

        Dictionary<string, X8664Symbol> PanicStubs { get; set; } = new Dictionary<string, X8664Symbol>(StringComparer.Ordinal);

        X8664Symbol Epilogue { get; set; }

        int SlotCount { get; set; }

        int MaxOutgoing { get; set; }

        int LabelCount { get; set; }

        public X8664FunctionEmitter(X8664Context context, IRFunction function)
        {
            this.Context = context;
            this.Function = function;
            this.Epilogue = NewLabel("epilogue");
        }

        public void Emit()
        {
            this.Assembler.Align(16);
            this.Assembler.Bind(this.Context.FunctionSymbol(this.Function));

            int start = this.Assembler.Position;

            this.Assembler.Push(X8664Register.RBP);
            this.Assembler.Mov(X8664Register.RBP, X8664Register.RSP);

            int prologuePosition = this.Assembler.Position;
            int probeFixup = this.Assembler.Fixups.Count;

            this.Assembler.MovImmediate(X8664Register.RAX, int.MaxValue);
            this.Assembler.Call(this.Context.Probe);
            this.Assembler.Alu(X8664AluOperation.Subtract, X8664Register.RSP, X8664Register.RAX);

            SpillParameters();
            EmitBlock(this.Function.Entryblock);

            this.Assembler.Bind(this.Epilogue);
            this.Assembler.Mov(X8664Register.RSP, X8664Register.RBP);
            this.Assembler.Pop(X8664Register.RBP);
            this.Assembler.Return();

            EmitPanicStubs();

            int frameSize = X8664Layout.AlignUp(16 * this.SlotCount, 16) + X8664Layout.AlignUp(this.MaxOutgoing, 16);

            if (frameSize < 4096)
            {
                if (this.Assembler.Fixups[probeFixup].Target != this.Context.Probe)
                {
                    throw ErrorHandler.Create($"The prologue of '{this.Function.Name}' could not be patched.");
                }

                this.Assembler.Fixups.RemoveAt(probeFixup);
                this.Assembler.PatchBytes(prologuePosition, [0x48, 0x81, 0xEC, .. BitConverter.GetBytes(frameSize), 0x66, 0x0F, 0x1F, 0x44, 0x00, 0x00]);
                this.Context.UnwindEntries.Add(new X8664UnwindEntry(start, this.Assembler.Position, frameSize, prologuePosition - start + 7));
            }
            else
            {
                this.Assembler.Patch32(prologuePosition + 1, frameSize);
                this.Context.UnwindEntries.Add(new X8664UnwindEntry(start, this.Assembler.Position, frameSize, prologuePosition - start + 13));
            }
        }

        public static void EmitProbe(X8664Context context)
        {
            X8664Assembler assembler = context.Assembler;
            X8664Symbol loop = new X8664Symbol("__probe.loop", X8664Sections.Text);
            X8664Symbol done = new X8664Symbol("__probe.done", X8664Sections.Text);

            assembler.Align(16);
            assembler.Bind(context.Probe);
            assembler.Mov(X8664Register.R10, X8664Register.RSP);
            assembler.Lea(X8664Register.R11, X8664Memory.At(X8664Register.R10, 8));
            assembler.Alu(X8664AluOperation.Subtract, X8664Register.R11, X8664Register.RAX);
            assembler.Bind(loop);
            assembler.AluImmediate(X8664AluOperation.Subtract, X8664Register.R10, 4096);
            assembler.Alu(X8664AluOperation.Compare, X8664Register.R10, X8664Register.R11);
            assembler.JumpIf(X8664Condition.Below, done);
            assembler.Test(X8664Memory.At(X8664Register.R10), X8664Register.RAX);
            assembler.Jump(loop);
            assembler.Bind(done);
            assembler.Test(X8664Memory.At(X8664Register.R11), X8664Register.RAX);
            assembler.Return();
        }

        #region Frame

        X8664Symbol NewLabel(string name) => new X8664Symbol($"{this.Function.Name}.{name}.{this.LabelCount++}", X8664Sections.Text);

        static X8664Memory Slot(int index) => X8664Memory.At(X8664Register.RBP, -16 * (index + 1));

        int NewSlot() => this.SlotCount++;

        X8664Memory SlotOf(IRLocal local)
        {
            if (!this.LocalSlots.TryGetValue(local, out int index))
            {
                index = NewSlot();
                this.LocalSlots[local] = index;
            }

            return Slot(index);
        }

        int AllocateTemporary() => this.FreeTemporaries.Count > 0 ? this.FreeTemporaries.Pop() : NewSlot();

        void FreeTemporary(int index) => this.FreeTemporaries.Push(index);

        X8664Memory GlobalMemory(IRGlobal global) => X8664Memory.Rip(this.Context.GlobalSymbol(global));

        X8664Symbol PanicStub(string message)
        {
            if (!this.PanicStubs.TryGetValue(message, out X8664Symbol? label))
            {
                label = NewLabel("panic");
                this.PanicStubs[message] = label;
            }

            return label;
        }

        void EmitPanicStubs()
        {
            foreach ((string message, X8664Symbol label) in this.PanicStubs)
            {
                this.Assembler.Bind(label);
                this.Assembler.Lea(X8664Register.RCX, X8664Memory.Rip(this.Context.Message(message)));
                this.Assembler.MovImmediate(X8664Register.RDX, Encoding.UTF8.GetByteCount(message));
                this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Panic));
            }

            if (this.PanicStubs.Count > 0)
            {
                this.MaxOutgoing = Math.Max(this.MaxOutgoing, 32);
            }
        }

        void SpillParameters()
        {
            int position = 0;

            foreach (IRLocal parameter in this.Function.Parameters)
            {
                X8664Memory slot = SlotOf(parameter);
                X8664ValueClass valueClass = X8664Layout.Classify(parameter.ValueType);

                if (valueClass == X8664ValueClass.Fat)
                {
                    SpillParameter(position++, X8664ValueClass.Integer, slot);
                    SpillParameter(position++, X8664ValueClass.Integer, slot.Offset(8));
                }
                else
                {
                    SpillParameter(position++, valueClass, slot);
                }
            }
        }

        void SpillParameter(int position, X8664ValueClass valueClass, X8664Memory destination)
        {
            if (position >= 4)
            {
                this.Assembler.Load(X8664Register.RAX, X8664Memory.At(X8664Register.RBP, 16 + 8 * position));
                this.Assembler.Store(destination, X8664Register.RAX);
                return;
            }

            switch (valueClass)
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Store(destination, IntegerArgumentRegisters[position]);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.StoreScalar(false, destination, FloatArgumentRegisters[position]);
                    break;

                case X8664ValueClass.Float64:
                    this.Assembler.StoreScalar(true, destination, FloatArgumentRegisters[position]);
                    break;
            }
        }

        #endregion

        #region Value movement

        void Normalize(IRType type, X8664Register register = X8664Register.RAX)
        {
            switch (type.DataType)
            {
                case IRDataTypes.Int8:
                    this.Assembler.MovSignExtend8(register, register);
                    break;

                case IRDataTypes.UInt8:
                case IRDataTypes.Bool:
                    this.Assembler.MovZeroExtend8(register, register);
                    break;

                case IRDataTypes.Int16:
                    this.Assembler.MovSignExtend16(register, register);
                    break;

                case IRDataTypes.UInt16:
                    this.Assembler.MovZeroExtend16(register, register);
                    break;

                case IRDataTypes.Int32:
                    this.Assembler.MovSignExtend32(register, register);
                    break;

                case IRDataTypes.UInt32:
                    this.Assembler.Mov32(register, register);
                    break;
            }
        }

        void LoadVariable(IRType type, X8664Memory memory, bool secondary = false)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Load(secondary ? X8664Register.RCX : X8664Register.RAX, memory);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.LoadScalar(false, secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, memory);
                    break;

                case X8664ValueClass.Float64:
                    this.Assembler.LoadScalar(true, secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, memory);
                    break;

                case X8664ValueClass.Fat:
                    if (secondary)
                    {
                        throw ErrorHandler.Create("Fat pointers cannot be loaded into secondary registers.");
                    }

                    this.Assembler.Load(X8664Register.RAX, memory);
                    this.Assembler.Load(X8664Register.RDX, memory.Offset(8));
                    break;
            }
        }

        void StoreVariable(IRType type, X8664Memory memory)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Store(memory, X8664Register.RAX);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.StoreScalar(false, memory, X8664XmmRegister.XMM0);
                    break;

                case X8664ValueClass.Float64:
                    this.Assembler.StoreScalar(true, memory, X8664XmmRegister.XMM0);
                    break;

                case X8664ValueClass.Fat:
                    this.Assembler.Store(memory, X8664Register.RAX);
                    this.Assembler.Store(memory.Offset(8), X8664Register.RDX);
                    break;
            }
        }

        void LoadTyped(IRType type, X8664Memory memory)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Load(X8664Layout.SizeOf(type), X8664Layout.IsSigned(type), X8664Register.RAX, memory);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.LoadScalar(false, X8664XmmRegister.XMM0, memory);
                    break;

                case X8664ValueClass.Float64:
                    this.Assembler.LoadScalar(true, X8664XmmRegister.XMM0, memory);
                    break;

                case X8664ValueClass.Fat:
                    if (memory.Symbol == null && memory.Base == X8664Register.RAX)
                    {
                        this.Assembler.Load(X8664Register.RDX, memory.Offset(8));
                        this.Assembler.Load(X8664Register.RAX, memory);
                    }
                    else
                    {
                        this.Assembler.Load(X8664Register.RAX, memory);
                        this.Assembler.Load(X8664Register.RDX, memory.Offset(8));
                    }
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot load a value of type '{type.Dump(0)}' from memory.");
            }
        }

        void StoreTyped(IRType type, X8664Memory memory)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Store(X8664Layout.SizeOf(type), memory, X8664Register.RAX);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.StoreScalar(false, memory, X8664XmmRegister.XMM0);
                    break;

                case X8664ValueClass.Float64:
                    this.Assembler.StoreScalar(true, memory, X8664XmmRegister.XMM0);
                    break;

                case X8664ValueClass.Fat:
                    this.Assembler.Store(memory, X8664Register.RAX);
                    this.Assembler.Store(memory.Offset(8), X8664Register.RDX);
                    break;

                default:
                    throw ErrorHandler.Create($"Cannot store a value of type '{type.Dump(0)}' to memory.");
            }
        }

        void MoveToSecondary(IRType type)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                    break;

                case X8664ValueClass.Float32:
                case X8664ValueClass.Float64:
                    this.Assembler.MovXmm(X8664XmmRegister.XMM1, X8664XmmRegister.XMM0);
                    break;

                default:
                    throw ErrorHandler.Create($"Values of type '{type.Dump(0)}' cannot be used as operands.");
            }
        }

        static void ExpectSameClass(IRType expected, IRType actual, string context)
        {
            if (X8664Layout.Classify(expected) != X8664Layout.Classify(actual))
            {
                throw ErrorHandler.Create($"Type mismatch in {context}: expected '{expected.Dump(0)}', but got '{actual.Dump(0)}'.");
            }
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
                    EmitReturn(returnInstruction);
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
                    this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                    this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Panic));
                    this.MaxOutgoing = Math.Max(this.MaxOutgoing, 32);
                    break;

                case IRBlock block:
                    EmitBlock(block);
                    break;

                case IRBytes bytes when bytes.ValueType.DataType == IRDataTypes.Void:
                    this.Assembler.Raw(bytes.Bytes);
                    break;

                case IRCall or IRCallIndirect or IRDispatch or IRAwait:
                    {
                        IRValueInstruction invocation = (IRValueInstruction)instruction;
                        EmitInvocation(invocation);

                        if (X8664Layout.Classify(invocation.ValueType) != X8664ValueClass.Void)
                        {
                            if (!this.ResultSlots.TryGetValue(invocation, out int slot))
                            {
                                slot = NewSlot();
                                this.ResultSlots[invocation] = slot;
                            }

                            StoreVariable(invocation.ValueType, Slot(slot));
                        }
                    }
                    break;

                case IRValueInstruction value:
                    EmitValue(value);
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"No x86-64 translation implemented for instruction '{instruction.GetType().Name}'");
            }
        }

        void EmitReturn(IRReturn returnInstruction)
        {
            if (returnInstruction.Values != null && returnInstruction.Values.Count > 0)
            {
                EmitValue(returnInstruction.Values[0]);
            }

            this.Assembler.Jump(this.Epilogue);
        }

        void EmitConditionalBranch(IRConditionalBranch conditionalBranch)
        {
            X8664Symbol elseLabel = NewLabel("else");
            X8664Symbol endLabel = NewLabel("endif");

            EmitValue(conditionalBranch.Condition);
            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
            this.Assembler.JumpIf(X8664Condition.Equal, conditionalBranch.HasElseBlock ? elseLabel : endLabel);

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
            X8664Symbol topLabel = NewLabel("loop");
            X8664Symbol continueLabel = NewLabel("continue");
            X8664Symbol exitLabel = NewLabel("exit");

            this.Assembler.Bind(topLabel);

            if (loop.Condition is not IRConstantInteger { Value: not 0 })
            {
                EmitValue(loop.Condition);
                this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
                this.Assembler.JumpIf(X8664Condition.Equal, exitLabel);
            }

            this.Loops.Push((continueLabel, exitLabel));
            EmitBlock(loop.Block);
            this.Loops.Pop();

            this.Assembler.Bind(continueLabel);

            if (loop.Increment != null)
            {
                EmitBlock(loop.Increment);
            }

            this.Assembler.Jump(topLabel);
            this.Assembler.Bind(exitLabel);
        }

        void EmitStore(IRStore store)
        {
            if (store.Offset == null)
            {
                switch (store.Target)
                {
                    case IRLocal local:
                        ExpectSameClass(local.ValueType, store.Value.ValueType, $"a store to '{local.Name}'");
                        EmitValue(store.Value);
                        StoreVariable(local.ValueType, SlotOf(local));
                        return;

                    case IRGlobal global:
                        ExpectSameClass(global.ValueType, store.Value.ValueType, $"a store to '{global.Name}'");
                        EmitValue(store.Value);
                        StoreVariable(global.ValueType, GlobalMemory(global));
                        return;
                }

                EmitValue(store.Target);
                int address = AllocateTemporary();
                this.Assembler.Store(Slot(address), X8664Register.RAX);

                EmitValue(store.Value);
                this.Assembler.Load(X8664Register.R11, Slot(address));
                FreeTemporary(address);

                StoreTyped(store.Value.ValueType, X8664Memory.At(X8664Register.R11));
                return;
            }

            EmitValue(store.Target);
            int pointer = AllocateTemporary();
            this.Assembler.Store(Slot(pointer), X8664Register.RAX);

            int? offsetSlot = null;
            int displacement = 0;
            bool nullCheck = false;
            IRType type = store.Value.ValueType;

            switch (store.Offset)
            {
                case IRStructProperty property:
                    displacement = X8664Layout.OffsetOf(property);
                    nullCheck = true;
                    ExpectSameClass(property.ValueType, store.Value.ValueType, $"a store to '{property.Name}'");
                    type = property.ValueType;
                    break;

                case IRConstantInteger constant:
                    displacement = (int)constant.Value;
                    break;

                default:
                    EmitValue(store.Offset);
                    offsetSlot = AllocateTemporary();
                    this.Assembler.Store(Slot(offsetSlot.Value), X8664Register.RAX);
                    break;
            }

            EmitValue(store.Value);

            this.Assembler.Load(X8664Register.R11, Slot(pointer));

            if (offsetSlot != null)
            {
                this.Assembler.Alu(X8664AluOperation.Add, X8664Register.R11, Slot(offsetSlot.Value));
                FreeTemporary(offsetSlot.Value);
            }

            if (nullCheck)
            {
                this.Assembler.Test(X8664Register.R11, X8664Register.R11);
                this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(NullReferenceMessage));
            }

            FreeTemporary(pointer);
            StoreTyped(type, X8664Memory.At(X8664Register.R11, displacement));
        }

        void EmitStoreElement(IRStoreElement store)
        {
            ExpectSameClass(store.ElementType, store.Value.ValueType, "an array element store");
            ExpectFat(store.Target);

            int width = X8664Layout.SizeOf(store.ElementType);

            EmitValue(store.Target);
            int array = AllocateTemporary();
            StoreVariable(store.Target.ValueType, Slot(array));

            EmitValue(store.Index);
            int index = AllocateTemporary();
            this.Assembler.Store(Slot(index), X8664Register.RAX);

            EmitValue(store.Value);

            this.Assembler.Load(X8664Register.R11, Slot(index));
            this.Assembler.Load(X8664Register.R10, Slot(array).Offset(8));
            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R11, X8664Register.R10);
            this.Assembler.JumpIf(X8664Condition.AboveOrEqual, PanicStub(IndexOutOfRangeMessage));

            if (width != 1)
            {
                this.Assembler.ImulImmediate(X8664Register.R11, X8664Register.R11, width);
            }

            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.R11, Slot(array));

            FreeTemporary(index);
            FreeTemporary(array);

            StoreTyped(store.ElementType, X8664Memory.At(X8664Register.R11));
        }

        static void ExpectFat(IRValueInstruction value)
        {
            if (X8664Layout.Classify(value.ValueType) != X8664ValueClass.Fat)
            {
                throw ErrorHandler.Create($"Expected an array or a string, but got '{value.ValueType.Dump(0)}'.");
            }
        }

        #endregion

        #region Values

        bool IsSimple(IRValueInstruction value)
        {
            if (X8664Layout.Classify(value.ValueType) == X8664ValueClass.Fat)
            {
                return false;
            }

            return this.ResultSlots.ContainsKey(value) || value is IRConstantInteger or IRLocal or IRGlobal or IRStructProperty;
        }

        void EmitSimpleSecondary(IRValueInstruction value)
        {
            if (this.ResultSlots.TryGetValue(value, out int slot))
            {
                LoadVariable(value.ValueType, Slot(slot), secondary: true);
                return;
            }

            switch (value)
            {
                case IRConstantInteger constant:
                    EmitConstant(constant, secondary: true);
                    break;

                case IRLocal local:
                    LoadVariable(local.ValueType, SlotOf(local), secondary: true);
                    break;

                case IRGlobal global:
                    LoadVariable(global.ValueType, GlobalMemory(global), secondary: true);
                    break;

                case IRStructProperty property:
                    this.Assembler.MovImmediate(X8664Register.RCX, X8664Layout.OffsetOf(property));
                    break;
            }
        }

        void EmitBinaryOperands(IRValueInstruction left, IRValueInstruction right)
        {
            EmitValue(left);

            if (IsSimple(right))
            {
                EmitSimpleSecondary(right);
                return;
            }

            int temporary = AllocateTemporary();
            StoreVariable(left.ValueType, Slot(temporary));

            EmitValue(right);
            MoveToSecondary(right.ValueType);

            LoadVariable(left.ValueType, Slot(temporary));
            FreeTemporary(temporary);
        }

        void EmitValue(IRValueInstruction value)
        {
            if (this.ResultSlots.TryGetValue(value, out int slot))
            {
                LoadVariable(value.ValueType, Slot(slot));
                return;
            }

            switch (value)
            {
                case IRConstantInteger constant:
                    EmitConstant(constant, secondary: false);
                    break;

                case IRConstantString constantString:
                    this.Assembler.Lea(X8664Register.RAX, X8664Memory.Rip(this.Context.ConstantSymbol(constantString)));
                    this.Assembler.MovImmediate(X8664Register.RDX, Encoding.UTF8.GetByteCount(constantString.Value));
                    break;

                case IRConstantBytes constantBytes:
                    this.Assembler.Lea(X8664Register.RAX, X8664Memory.Rip(this.Context.ConstantSymbol(constantBytes)));
                    this.Assembler.MovImmediate(X8664Register.RDX, constantBytes.Value.Length);
                    break;

                case IRLocal local:
                    LoadVariable(local.ValueType, SlotOf(local));
                    break;

                case IRGlobal global:
                    LoadVariable(global.ValueType, GlobalMemory(global));
                    break;

                case IRStructProperty property:
                    this.Assembler.MovImmediate(X8664Register.RAX, X8664Layout.OffsetOf(property));
                    break;

                case IRFunctionReference functionReference:
                    this.Assembler.Lea(X8664Register.RAX, X8664Memory.Rip(this.Context.FunctionSymbol(functionReference.Function)));
                    break;

                case IRAdd add:
                    EmitArithmetic(X8664SseOperation.Add, add.Left, add.Right, add.ValueType);
                    break;

                case IRSubtract subtract:
                    EmitArithmetic(X8664SseOperation.Subtract, subtract.Left, subtract.Right, subtract.ValueType);
                    break;

                case IRMultiply multiply:
                    EmitArithmetic(X8664SseOperation.Multiply, multiply.Left, multiply.Right, multiply.ValueType);
                    break;

                case IRDivide divide:
                    EmitArithmetic(X8664SseOperation.Divide, divide.Left, divide.Right, divide.ValueType);
                    break;

                case IRBitwise bitwise:
                    EmitBitwise(bitwise);
                    break;

                case IRCompare compare:
                    EmitCompare(compare);
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
                    ExpectFat(length.Target);
                    EmitValue(length.Target);
                    this.Assembler.Mov(X8664Register.RAX, X8664Register.RDX);
                    Normalize(length.ValueType);
                    break;

                case IRFatPointer fatPointer:
                    {
                        EmitValue(fatPointer.Length);
                        int length = AllocateTemporary();
                        this.Assembler.Store(Slot(length), X8664Register.RAX);

                        EmitValue(fatPointer.Pointer);
                        this.Assembler.Load(X8664Register.RDX, Slot(length));
                        FreeTemporary(length);
                    }
                    break;

                case IRCall or IRCallIndirect or IRDispatch or IRAwait:
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
                    this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                    CallMalloc();
                    break;

                case IRBytes bytes:
                    this.Assembler.Raw(bytes.Bytes);
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"No x86-64 translation implemented for instruction '{value.GetType().Name}'");
            }
        }

        void EmitConstant(IRConstantInteger constant, bool secondary)
        {
            switch (constant.IntegerType)
            {
                case IRDataTypes.Float32:
                    {
                        X8664Register scratch = secondary ? X8664Register.R11 : X8664Register.RAX;
                        this.Assembler.MovImmediate(scratch, (uint)BitConverter.SingleToInt32Bits((float)constant.Value));
                        this.Assembler.MovToXmm(secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, scratch);
                    }
                    break;

                case IRDataTypes.Float64:
                    {
                        X8664Register scratch = secondary ? X8664Register.R11 : X8664Register.RAX;
                        this.Assembler.MovImmediate(scratch, BitConverter.DoubleToInt64Bits(constant.Value));
                        this.Assembler.MovToXmm(secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, scratch);
                    }
                    break;

                default:
                    this.Assembler.MovImmediate(secondary ? X8664Register.RCX : X8664Register.RAX, X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value));
                    break;
            }
        }

        void EmitArithmetic(X8664SseOperation operation, IRValueInstruction left, IRValueInstruction right, IRType type)
        {
            X8664ValueClass valueClass = X8664Layout.Classify(type);

            EmitBinaryOperands(left, right);

            switch (valueClass)
            {
                case X8664ValueClass.Float32:
                case X8664ValueClass.Float64:
                    this.Assembler.ScalarArithmetic(operation, valueClass == X8664ValueClass.Float64, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                    return;

                case X8664ValueClass.Integer:
                    break;

                default:
                    throw ErrorHandler.Create($"Arithmetic is not supported on values of type '{type.Dump(0)}'.");
            }

            switch (operation)
            {
                case X8664SseOperation.Add:
                    this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, X8664Register.RCX);
                    break;

                case X8664SseOperation.Subtract:
                    this.Assembler.Alu(X8664AluOperation.Subtract, X8664Register.RAX, X8664Register.RCX);
                    break;

                case X8664SseOperation.Multiply:
                    this.Assembler.Imul(X8664Register.RAX, X8664Register.RCX);
                    break;

                case X8664SseOperation.Divide:
                    this.Assembler.Test(X8664Register.RCX, X8664Register.RCX);
                    this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(DivideByZeroMessage));

                    if (X8664Layout.IsSigned(type))
                    {
                        this.Assembler.Cqo();
                        this.Assembler.Divide(true, X8664Register.RCX);
                    }
                    else
                    {
                        this.Assembler.Alu(X8664AluOperation.Xor, X8664Register.RDX, X8664Register.RDX);
                        this.Assembler.Divide(false, X8664Register.RCX);
                    }
                    break;
            }

            Normalize(type);
        }

        void EmitBitwise(IRBitwise bitwise)
        {
            if (X8664Layout.Classify(bitwise.ValueType) != X8664ValueClass.Integer)
            {
                throw ErrorHandler.Create($"Bitwise operations are not supported on values of type '{bitwise.ValueType.Dump(0)}'.");
            }

            EmitBinaryOperands(bitwise.Left, bitwise.Right);

            switch (bitwise.Operator)
            {
                case IRBitwiseOperator.And:
                    this.Assembler.Alu(X8664AluOperation.And, X8664Register.RAX, X8664Register.RCX);
                    break;

                case IRBitwiseOperator.Or:
                    this.Assembler.Alu(X8664AluOperation.Or, X8664Register.RAX, X8664Register.RCX);
                    break;

                case IRBitwiseOperator.Xor:
                    this.Assembler.Alu(X8664AluOperation.Xor, X8664Register.RAX, X8664Register.RCX);
                    break;

                case IRBitwiseOperator.ShiftLeft:
                case IRBitwiseOperator.ShiftRight:
                    if (X8664Layout.SizeOf(bitwise.ValueType) <= 4)
                    {
                        this.Assembler.AluImmediate(X8664AluOperation.And, X8664Register.RCX, 31);
                    }

                    this.Assembler.Shift(bitwise.Operator == IRBitwiseOperator.ShiftLeft
                        ? X8664ShiftOperation.ShiftLeft
                        : X8664Layout.IsSigned(bitwise.ValueType) ? X8664ShiftOperation.ShiftRightArithmetic : X8664ShiftOperation.ShiftRightLogical,
                        X8664Register.RAX);
                    break;
            }

            Normalize(bitwise.ValueType);
        }

        void EmitCompare(IRCompare compare)
        {
            X8664ValueClass valueClass = X8664Layout.Classify(compare.OperandType);

            if (valueClass == X8664ValueClass.Fat)
            {
                EmitFatCompare(compare);
                return;
            }

            EmitBinaryOperands(compare.Left, compare.Right);

            if (valueClass is X8664ValueClass.Float32 or X8664ValueClass.Float64)
            {
                bool isDouble = valueClass == X8664ValueClass.Float64;

                switch (compare.Operator)
                {
                    case IRComparisonOperator.GreaterThan:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                        this.Assembler.SetCondition(X8664Condition.Above, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.GreaterThanOrEqual:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                        this.Assembler.SetCondition(X8664Condition.AboveOrEqual, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.LessThan:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM1, X8664XmmRegister.XMM0);
                        this.Assembler.SetCondition(X8664Condition.Above, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.LessThanOrEqual:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM1, X8664XmmRegister.XMM0);
                        this.Assembler.SetCondition(X8664Condition.AboveOrEqual, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.EqualTo:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                        this.Assembler.SetCondition(X8664Condition.Equal, X8664Register.RAX);
                        this.Assembler.SetCondition(X8664Condition.NoParity, X8664Register.RCX);
                        this.Assembler.Alu(X8664AluOperation.And, X8664Register.RAX, X8664Register.RCX);
                        break;

                    case IRComparisonOperator.NotEqualTo:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                        this.Assembler.SetCondition(X8664Condition.NotEqual, X8664Register.RAX);
                        this.Assembler.SetCondition(X8664Condition.Parity, X8664Register.RCX);
                        this.Assembler.Alu(X8664AluOperation.Or, X8664Register.RAX, X8664Register.RCX);
                        break;
                }

                this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
                return;
            }

            bool signed = X8664Layout.IsSigned(compare.OperandType);

            X8664Condition condition = compare.Operator switch
            {
                IRComparisonOperator.EqualTo => X8664Condition.Equal,
                IRComparisonOperator.NotEqualTo => X8664Condition.NotEqual,
                IRComparisonOperator.LessThan => signed ? X8664Condition.Less : X8664Condition.Below,
                IRComparisonOperator.LessThanOrEqual => signed ? X8664Condition.LessOrEqual : X8664Condition.BelowOrEqual,
                IRComparisonOperator.GreaterThan => signed ? X8664Condition.Greater : X8664Condition.Above,
                IRComparisonOperator.GreaterThanOrEqual => signed ? X8664Condition.GreaterOrEqual : X8664Condition.AboveOrEqual,
                _ => throw ErrorHandler.CreateNotImplimented($"Comparison '{compare.Operator}' is not implemented.")
            };

            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.SetCondition(condition, X8664Register.RAX);
            this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
        }

        void EmitFatCompare(IRCompare compare)
        {
            if (compare.Operator is not (IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo))
            {
                throw ErrorHandler.Create($"Only equality comparisons are supported on values of type '{compare.OperandType.Dump(0)}'.");
            }

            EmitValue(compare.Left);
            int temporary = AllocateTemporary();
            StoreVariable(compare.Left.ValueType, Slot(temporary));

            EmitValue(compare.Right);
            this.Assembler.Load(X8664Register.RCX, Slot(temporary));
            this.Assembler.Load(X8664Register.R8, Slot(temporary).Offset(8));
            FreeTemporary(temporary);

            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.SetCondition(X8664Condition.Equal, X8664Register.RAX);
            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RDX, X8664Register.R8);
            this.Assembler.SetCondition(X8664Condition.Equal, X8664Register.RCX);
            this.Assembler.Alu(X8664AluOperation.And, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);

            if (compare.Operator == IRComparisonOperator.NotEqualTo)
            {
                this.Assembler.AluImmediate(X8664AluOperation.Xor, X8664Register.RAX, 1);
            }
        }

        void EmitConvert(IRConvert convert)
        {
            IRType source = convert.Value.ValueType;
            IRType target = convert.ValueType;

            X8664ValueClass from = X8664Layout.Classify(source);
            X8664ValueClass to = X8664Layout.Classify(target);

            EmitValue(convert.Value);

            switch (from, to)
            {
                case (X8664ValueClass.Integer, X8664ValueClass.Integer):
                    if (target.DataType == IRDataTypes.Bool)
                    {
                        this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
                        this.Assembler.SetCondition(X8664Condition.NotEqual, X8664Register.RAX);
                        this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
                    }
                    else
                    {
                        Normalize(target);
                    }
                    break;

                case (X8664ValueClass.Integer, X8664ValueClass.Float32):
                case (X8664ValueClass.Integer, X8664ValueClass.Float64):
                    {
                        bool isDouble = to == X8664ValueClass.Float64;

                        if (source.DataType is IRDataTypes.UInt64 or IRDataTypes.Pointer)
                        {
                            X8664Symbol large = NewLabel("convert.large");
                            X8664Symbol done = NewLabel("convert.done");

                            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
                            this.Assembler.JumpIf(X8664Condition.Sign, large);
                            this.Assembler.ConvertIntegerToScalar(isDouble, X8664XmmRegister.XMM0, X8664Register.RAX);
                            this.Assembler.Jump(done);

                            this.Assembler.Bind(large);
                            this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightLogical, X8664Register.RCX, 1);
                            this.Assembler.AluImmediate(X8664AluOperation.And, X8664Register.RAX, 1);
                            this.Assembler.Alu(X8664AluOperation.Or, X8664Register.RCX, X8664Register.RAX);
                            this.Assembler.ConvertIntegerToScalar(isDouble, X8664XmmRegister.XMM0, X8664Register.RCX);
                            this.Assembler.ScalarArithmetic(X8664SseOperation.Add, isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM0);

                            this.Assembler.Bind(done);
                        }
                        else
                        {
                            this.Assembler.ConvertIntegerToScalar(isDouble, X8664XmmRegister.XMM0, X8664Register.RAX);
                        }
                    }
                    break;

                case (X8664ValueClass.Float32, X8664ValueClass.Integer):
                case (X8664ValueClass.Float64, X8664ValueClass.Integer):
                    {
                        bool isDouble = from == X8664ValueClass.Float64;

                        if (target.DataType == IRDataTypes.Bool)
                        {
                            this.Assembler.Xorps(X8664XmmRegister.XMM1, X8664XmmRegister.XMM1);
                            this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                            this.Assembler.SetCondition(X8664Condition.NotEqual, X8664Register.RAX);
                            this.Assembler.SetCondition(X8664Condition.Parity, X8664Register.RCX);
                            this.Assembler.Alu(X8664AluOperation.Or, X8664Register.RAX, X8664Register.RCX);
                            this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
                        }
                        else
                        {
                            this.Assembler.ConvertScalarToInteger(isDouble, X8664Register.RAX, X8664XmmRegister.XMM0);
                            Normalize(target);
                        }
                    }
                    break;

                case (X8664ValueClass.Float32, X8664ValueClass.Float64):
                    this.Assembler.ConvertSingleToDouble(X8664XmmRegister.XMM0, X8664XmmRegister.XMM0);
                    break;

                case (X8664ValueClass.Float64, X8664ValueClass.Float32):
                    this.Assembler.ConvertDoubleToSingle(X8664XmmRegister.XMM0, X8664XmmRegister.XMM0);
                    break;

                case (X8664ValueClass.Float32, X8664ValueClass.Float32):
                case (X8664ValueClass.Float64, X8664ValueClass.Float64):
                case (X8664ValueClass.Fat, X8664ValueClass.Fat):
                case (X8664ValueClass.Fat, X8664ValueClass.Integer):
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"Converting '{source.Dump(0)}' to '{target.Dump(0)}' is not supported when targeting x86-64.");
            }
        }

        void EmitLoad(IRLoad load)
        {
            if (load.Offset == null)
            {
                switch (load.Target)
                {
                    case IRLocal local:
                        LoadVariable(load.ValueType, SlotOf(local));
                        return;

                    case IRGlobal global:
                        LoadVariable(load.ValueType, GlobalMemory(global));
                        return;
                }

                EmitValue(load.Target);
                LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX));
                return;
            }

            EmitValue(load.Target);

            switch (load.Offset)
            {
                case IRStructProperty property:
                    this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
                    this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(NullReferenceMessage));
                    LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX, X8664Layout.OffsetOf(property)));
                    return;

                case IRConstantInteger constant:
                    LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX, (int)constant.Value));
                    return;
            }

            int pointer = AllocateTemporary();
            this.Assembler.Store(Slot(pointer), X8664Register.RAX);

            EmitValue(load.Offset);
            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, Slot(pointer));
            FreeTemporary(pointer);

            LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX));
        }

        void EmitLoadElement(IRLoadElement load)
        {
            ExpectFat(load.Target);

            int width = X8664Layout.SizeOf(load.ElementType);

            EmitValue(load.Target);
            int array = AllocateTemporary();
            StoreVariable(load.Target.ValueType, Slot(array));

            EmitValue(load.Index);
            this.Assembler.Load(X8664Register.RCX, Slot(array).Offset(8));
            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.JumpIf(X8664Condition.AboveOrEqual, PanicStub(IndexOutOfRangeMessage));

            if (width != 1)
            {
                this.Assembler.ImulImmediate(X8664Register.RAX, X8664Register.RAX, width);
            }

            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, Slot(array));
            FreeTemporary(array);

            LoadTyped(load.ElementType, X8664Memory.At(X8664Register.RAX));
        }

        void CallMalloc()
        {
            this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Malloc));
            this.MaxOutgoing = Math.Max(this.MaxOutgoing, 32);
        }

        void EmitInstantiateStruct(IRInstantiateStruct instantiate)
        {
            this.Assembler.MovImmediate(X8664Register.RCX, X8664Layout.SizeOf(instantiate.StructSchema));
            CallMalloc();

            if (instantiate.PropertyValues.Count == 0)
            {
                return;
            }

            int pointer = AllocateTemporary();
            this.Assembler.Store(Slot(pointer), X8664Register.RAX);

            for (int i = 0; i < instantiate.PropertyValues.Count; i++)
            {
                IRStructProperty property = instantiate.StructSchema.Properties[i];
                IRValueInstruction propertyValue = instantiate.PropertyValues[i];

                ExpectSameClass(property.ValueType, propertyValue.ValueType, $"the initialization of '{property.Name}'");

                EmitValue(propertyValue);
                this.Assembler.Load(X8664Register.R11, Slot(pointer));
                StoreTyped(property.ValueType, X8664Memory.At(X8664Register.R11, X8664Layout.OffsetOf(property)));
            }

            this.Assembler.Load(X8664Register.RAX, Slot(pointer));
            FreeTemporary(pointer);
        }

        void EmitInstantiateArray(IRInstantiateArray instantiate)
        {
            IRType elementType = instantiate.ArraySchema.ElementType;
            int width = X8664Layout.SizeOf(elementType);

            EmitValue(instantiate.Size);
            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
            this.Assembler.JumpIf(X8664Condition.Sign, PanicStub(NegativeSizeMessage));

            int length = AllocateTemporary();
            this.Assembler.Store(Slot(length), X8664Register.RAX);

            this.Assembler.ImulImmediate(X8664Register.RCX, X8664Register.RAX, width);
            CallMalloc();

            int pointer = AllocateTemporary();
            this.Assembler.Store(Slot(pointer), X8664Register.RAX);

            for (int i = 0; i < instantiate.ElementValues.Count; i++)
            {
                ExpectSameClass(elementType, instantiate.ElementValues[i].ValueType, "an array initializer");

                EmitValue(instantiate.ElementValues[i]);
                this.Assembler.Load(X8664Register.R11, Slot(pointer));
                StoreTyped(elementType, X8664Memory.At(X8664Register.R11, i * width));
            }

            this.Assembler.Load(X8664Register.RAX, Slot(pointer));
            this.Assembler.Load(X8664Register.RDX, Slot(length));

            FreeTemporary(pointer);
            FreeTemporary(length);
        }

        void EmitInvocation(IRValueInstruction invocation)
        {
            switch (invocation)
            {
                case IRCall call when call.Function is IRFunctionImport functionImport:
                    EmitCall(call.Arguments, true, null, () => this.Assembler.Call(X8664Memory.Rip(this.Context.ImportSymbol(functionImport))), call.ValueType);
                    break;

                case IRCall call:
                    EmitCall(call.Arguments, false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(call.Function)), call.ValueType);
                    break;

                case IRCallIndirect indirect:
                    EmitCall(indirect.Arguments, true, indirect.FunctionPointer, null, indirect.ValueType);
                    break;

                case IRDispatch dispatch:
                    {
                        if (!this.Context.Dispatchers.TryGetValue(dispatch.Kernel, out IRFunction? dispatcher))
                        {
                            throw ErrorHandler.Create($"No dispatcher was generated for kernel '{dispatch.Kernel.Name}'.");
                        }

                        EmitCall([dispatch.Count, .. dispatch.Arguments], false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(dispatcher)), dispatch.ValueType);
                    }
                    break;

                case IRAwait await:
                    EmitCall([await.Handle], false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Await)), await.ValueType);
                    break;

                default:
                    throw ErrorHandler.Create($"'{invocation.GetType().Name}' is not an invocation.");
            }
        }

        void EmitCall(List<IRValueInstruction> arguments, bool nativeAbi, IRValueInstruction? functionPointer, Action? emitCall, IRType returnType)
        {
            List<int> temporaries = new List<int>();
            List<(X8664Memory Source, X8664ValueClass Class)> positions = new List<(X8664Memory Source, X8664ValueClass Class)>();

            int? pointerTemporary = null;

            if (functionPointer != null)
            {
                EmitValue(functionPointer);
                pointerTemporary = AllocateTemporary();
                this.Assembler.Store(Slot(pointerTemporary.Value), X8664Register.RAX);
            }

            foreach (IRValueInstruction argument in arguments)
            {
                EmitValue(argument);

                int temporary = AllocateTemporary();
                temporaries.Add(temporary);
                StoreVariable(argument.ValueType, Slot(temporary));

                X8664ValueClass valueClass = X8664Layout.Classify(argument.ValueType);

                if (valueClass == X8664ValueClass.Fat)
                {
                    positions.Add((Slot(temporary), X8664ValueClass.Integer));

                    if (!nativeAbi)
                    {
                        positions.Add((Slot(temporary).Offset(8), X8664ValueClass.Integer));
                    }
                }
                else
                {
                    positions.Add((Slot(temporary), valueClass));
                }
            }

            for (int i = 4; i < positions.Count; i++)
            {
                this.Assembler.Load(X8664Register.RAX, positions[i].Source);
                this.Assembler.Store(X8664Memory.At(X8664Register.RSP, 8 * i), X8664Register.RAX);
            }

            for (int i = 0; i < Math.Min(4, positions.Count); i++)
            {
                switch (positions[i].Class)
                {
                    case X8664ValueClass.Integer:
                        this.Assembler.Load(IntegerArgumentRegisters[i], positions[i].Source);
                        break;

                    case X8664ValueClass.Float32:
                        this.Assembler.LoadScalar(false, FloatArgumentRegisters[i], positions[i].Source);
                        break;

                    case X8664ValueClass.Float64:
                        this.Assembler.LoadScalar(true, FloatArgumentRegisters[i], positions[i].Source);
                        break;
                }
            }

            if (pointerTemporary != null)
            {
                this.Assembler.Load(X8664Register.R11, Slot(pointerTemporary.Value));
                this.Assembler.Call(X8664Register.R11);
            }
            else
            {
                emitCall!();
            }

            this.MaxOutgoing = Math.Max(this.MaxOutgoing, 32 + 8 * Math.Max(0, positions.Count - 4));

            for (int i = temporaries.Count - 1; i >= 0; i--)
            {
                FreeTemporary(temporaries[i]);
            }

            if (pointerTemporary != null)
            {
                FreeTemporary(pointerTemporary.Value);
            }

            if (nativeAbi && X8664Layout.Classify(returnType) == X8664ValueClass.Integer)
            {
                Normalize(returnType);
            }
        }

        #endregion
    }
}
