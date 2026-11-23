using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Model;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using CommonIR.Passes.Optimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal partial class X8664FunctionEmitter
    {
        public const string DivideByZeroMessage = "Attempted to divide by zero.";
        public const string IndexOutOfRangeMessage = "Index was outside the bounds of the array.";
        public const string NullReferenceMessage = "Object reference not set to an instance of an object.";
        public const string NegativeSizeMessage = "Arrays cannot have a negative size.";

        static readonly X8664Register[] IntegerArgumentRegisters = [X8664Register.RCX, X8664Register.RDX, X8664Register.R8, X8664Register.R9];

        static readonly X8664XmmRegister[] FloatArgumentRegisters = [X8664XmmRegister.XMM0, X8664XmmRegister.XMM1, X8664XmmRegister.XMM2, X8664XmmRegister.XMM3];

        static readonly X8664Register[] CalleeSavedIntegers = [X8664Register.RBX, X8664Register.RSI, X8664Register.RDI, X8664Register.R12, X8664Register.R13, X8664Register.R14, X8664Register.R15];

        static readonly X8664XmmRegister[] CalleeSavedFloats = [X8664XmmRegister.XMM6, X8664XmmRegister.XMM7, X8664XmmRegister.XMM8, X8664XmmRegister.XMM9, X8664XmmRegister.XMM10, X8664XmmRegister.XMM11, X8664XmmRegister.XMM12, X8664XmmRegister.XMM13, X8664XmmRegister.XMM14, X8664XmmRegister.XMM15];

        sealed class Home
        {
            public X8664ValueClass Class { get; init; }

            public X8664Register Low { get; init; }

            public X8664Register High { get; init; }

            public X8664XmmRegister Xmm { get; init; }
        }

        sealed class Temporary
        {
            public X8664ValueClass Class { get; init; }

            public bool InRegister { get; set; }

            public X8664Register Low { get; set; }

            public X8664Register High { get; set; }

            public X8664XmmRegister Xmm { get; set; }

            public int Slot { get; set; } = -1;
        }

        readonly struct Operand
        {
            public X8664Register Register { get; init; }

            public X8664XmmRegister Xmm { get; init; }

            public long? Immediate { get; init; }
        }

        sealed class FatSource
        {
            public bool InRegisters { get; init; }

            public X8664Register Pointer { get; init; }

            public X8664Register Length { get; init; }

            public X8664Memory Memory { get; init; }

            public Temporary? Temporary { get; init; }
        }

        X8664Context Context { get; set; }

        X8664Assembler Assembler { get; set; } = null!;

        IRFunction Function { get; set; }

        Dictionary<IRLocal, Home> Homes { get; } = new Dictionary<IRLocal, Home>();

        Dictionary<IRLocal, int> LocalSlots { get; set; } = new Dictionary<IRLocal, int>();

        Dictionary<IRInstruction, int> ResultSlots { get; set; } = new Dictionary<IRInstruction, int>();

        Stack<int> FreeSlots { get; set; } = new Stack<int>();

        List<X8664Register> IntegerPool { get; set; } = new List<X8664Register>();

        List<X8664XmmRegister> FloatPool { get; set; } = new List<X8664XmmRegister>();

        HashSet<X8664Register> UsedIntegers { get; } = new HashSet<X8664Register>();

        HashSet<X8664XmmRegister> UsedFloats { get; } = new HashSet<X8664XmmRegister>();

        List<X8664Register> SavedIntegers { get; set; } = new List<X8664Register>();

        List<X8664XmmRegister> SavedFloats { get; set; } = new List<X8664XmmRegister>();

        HashSet<IRLoadElement> CheckedLoads { get; } = new HashSet<IRLoadElement>(ReferenceEqualityComparer.Instance);

        Dictionary<IRLocal, long> KnownMultiples { get; } = new Dictionary<IRLocal, long>(ReferenceEqualityComparer.Instance);

        HashSet<IRLocal> NonNull { get; } = new HashSet<IRLocal>(ReferenceEqualityComparer.Instance);

        Stack<(X8664Symbol Continue, X8664Symbol Exit)> Loops { get; set; } = new Stack<(X8664Symbol Continue, X8664Symbol Exit)>();

        Dictionary<string, X8664Symbol> PanicStubs { get; set; } = new Dictionary<string, X8664Symbol>(StringComparer.Ordinal);

        X8664Symbol Epilogue { get; set; } = null!;

        int SlotCount { get; set; }

        int MaxOutgoing { get; set; }

        int LabelCount { get; set; }

        int SaveArea => 16 * this.SavedFloats.Count + (this.SavedIntegers.Count % 2 == 1 ? 8 : 0);

        int ArgumentBase => this.SaveArea + 8 * this.SavedIntegers.Count + 16;

        public X8664FunctionEmitter(X8664Context context, IRFunction function)
        {
            this.Context = context;
            this.Function = function;
        }

        public void Emit()
        {
            AssignHomes();

            EmitPass(new X8664Assembler(), real: false);

            this.SavedIntegers = CalleeSavedIntegers.Where(this.UsedIntegers.Contains).ToList();
            this.SavedFloats = CalleeSavedFloats.Where(this.UsedFloats.Contains).ToList();

            EmitPass(this.Context.Assembler, real: true);
        }

        void EmitPass(X8664Assembler assembler, bool real)
        {
            this.Assembler = assembler;
            this.NonNull.Clear();
            this.Assembler.Joined = this.NonNull.Clear;
            this.LocalSlots = new Dictionary<IRLocal, int>();
            this.ResultSlots = new Dictionary<IRInstruction, int>();
            this.FreeSlots = new Stack<int>();
            this.Loops = new Stack<(X8664Symbol Continue, X8664Symbol Exit)>();
            this.PanicStubs = new Dictionary<string, X8664Symbol>(StringComparer.Ordinal);
            this.CheckedLoads.Clear();
            this.KnownMultiples.Clear();
            this.SlotCount = 0;
            this.MaxOutgoing = 0;
            this.LabelCount = 0;
            this.Epilogue = NewLabel("epilogue");

            HashSet<X8664Register> homeIntegers = this.Homes.Values.SelectMany(h => h.Class == X8664ValueClass.Fat ? new[] { h.Low, h.High } : h.Class == X8664ValueClass.Integer ? new[] { h.Low } : []).ToHashSet();
            HashSet<X8664XmmRegister> homeFloats = this.Homes.Values.Where(h => h.Class is X8664ValueClass.Float32 or X8664ValueClass.Float64).Select(h => h.Xmm).ToHashSet();

            this.IntegerPool = CalleeSavedIntegers.Where(r => !homeIntegers.Contains(r)).Reverse().ToList();
            this.FloatPool = CalleeSavedFloats.Where(r => !homeFloats.Contains(r)).Reverse().ToList();
            this.UsedIntegers.UnionWith(homeIntegers);
            this.UsedFloats.UnionWith(homeFloats);

            this.Assembler.Align(16);

            if (real)
            {
                this.Assembler.Bind(this.Context.FunctionSymbol(this.Function));
            }

            int start = this.Assembler.Position;
            X8664UnwindEntry unwind = new X8664UnwindEntry(start);

            this.Assembler.Push(X8664Register.RBP);
            unwind.Push(this.Assembler.Position - start, X8664Register.RBP);

            foreach (X8664Register register in this.SavedIntegers)
            {
                this.Assembler.Push(register);
                unwind.Push(this.Assembler.Position - start, register);
            }

            if (this.SaveArea > 0)
            {
                this.Assembler.AluImmediate(X8664AluOperation.Subtract, X8664Register.RSP, this.SaveArea);
                unwind.Allocate(this.Assembler.Position - start, this.SaveArea);
            }

            for (int i = 0; i < this.SavedFloats.Count; i++)
            {
                this.Assembler.StorePacked(X8664Memory.At(X8664Register.RSP, 16 * i), this.SavedFloats[i]);
                unwind.SaveXmm(this.Assembler.Position - start, this.SavedFloats[i], 16 * i);
            }

            this.Assembler.Mov(X8664Register.RBP, X8664Register.RSP);
            unwind.SetFramePointer(this.Assembler.Position - start);

            int prologuePosition = this.Assembler.Position;
            int probeFixup = this.Assembler.Fixups.Count;

            this.Assembler.MovImmediate(X8664Register.RAX, int.MaxValue);
            this.Assembler.Call(this.Context.Probe);
            this.Assembler.Alu(X8664AluOperation.Subtract, X8664Register.RSP, X8664Register.RAX);

            SpillParameters();
            EmitBlock(this.Function.Entryblock);

            this.Assembler.Bind(this.Epilogue);

            for (int i = 0; i < this.SavedFloats.Count; i++)
            {
                this.Assembler.LoadPacked(this.SavedFloats[i], X8664Memory.At(X8664Register.RBP, 16 * i));
            }

            if (this.SaveArea > 0)
            {
                this.Assembler.Lea(X8664Register.RSP, X8664Memory.At(X8664Register.RBP, this.SaveArea));
            }
            else
            {
                this.Assembler.Mov(X8664Register.RSP, X8664Register.RBP);
            }

            for (int i = this.SavedIntegers.Count - 1; i >= 0; i--)
            {
                this.Assembler.Pop(this.SavedIntegers[i]);
            }

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
                unwind.PrologSize = prologuePosition - start + 7;
            }
            else
            {
                this.Assembler.Patch32(prologuePosition + 1, frameSize);
                unwind.PrologSize = prologuePosition - start + 13;
            }

            unwind.Allocate(unwind.PrologSize, frameSize);
            unwind.End = this.Assembler.Position;

            if (real)
            {
                this.Context.UnwindEntries.Add(unwind);
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

        #region Register homes

        void AssignHomes()
        {
            Dictionary<IRLocal, long> weights = new Dictionary<IRLocal, long>(ReferenceEqualityComparer.Instance);

            void CountValue(IRValueInstruction value, long weight)
            {
                if (value is IRLocal local)
                {
                    weights[local] = weights.GetValueOrDefault(local) + weight;
                    return;
                }

                foreach (IRValueInstruction child in IRTree.Children(value))
                {
                    CountValue(child, weight);
                }
            }

            void CountBlock(IRBlock block, long weight)
            {
                foreach (IRInstruction instruction in block.Instructions)
                {
                    switch (instruction)
                    {
                        case IRLoop loop:
                            {
                                long inner = Math.Min(weight * 8, 1L << 40);
                                CountValue(loop.Condition, inner);
                                CountBlock(loop.Block, inner);

                                if (loop.Increment != null)
                                {
                                    CountBlock(loop.Increment, inner);
                                }
                            }
                            break;

                        case IRConditionalBranch branch:
                            CountValue(branch.Condition, weight);
                            CountBlock(branch.ThenBlock, weight);

                            if (branch.HasElseBlock && branch.ElseBlock != null)
                            {
                                CountBlock(branch.ElseBlock, weight);
                            }
                            break;

                        case IRBlock nested:
                            CountBlock(nested, weight);
                            break;

                        default:
                            foreach (IRValueInstruction child in IRTree.Children(instruction))
                            {
                                CountValue(child, weight);
                            }
                            break;
                    }
                }
            }

            CountBlock(this.Function.Entryblock, 1);

            Queue<X8664Register> integers = new Queue<X8664Register>(CalleeSavedIntegers);
            Queue<X8664XmmRegister> floats = new Queue<X8664XmmRegister>(CalleeSavedFloats);

            foreach ((IRLocal local, long weight) in weights.OrderByDescending(p => p.Value))
            {
                if (weight < 3)
                {
                    break;
                }

                X8664ValueClass valueClass = X8664Layout.Classify(local.ValueType);

                switch (valueClass)
                {
                    case X8664ValueClass.Integer when integers.Count > 0:
                        this.Homes[local] = new Home { Class = valueClass, Low = integers.Dequeue() };
                        break;

                    case X8664ValueClass.Fat when integers.Count > 1:
                        this.Homes[local] = new Home { Class = valueClass, Low = integers.Dequeue(), High = integers.Dequeue() };
                        break;

                    case X8664ValueClass.Float32 or X8664ValueClass.Float64 when floats.Count > 0:
                        this.Homes[local] = new Home { Class = valueClass, Xmm = floats.Dequeue() };
                        break;
                }
            }
        }

        Home? RegisterHome(IRValueInstruction value) => value is IRLocal local && this.Homes.TryGetValue(local, out Home? home) ? home : null;

        #endregion

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

        Temporary AllocateTemporary(X8664ValueClass valueClass)
        {
            Temporary temporary = new Temporary { Class = valueClass };

            switch (valueClass)
            {
                case X8664ValueClass.Integer when this.IntegerPool.Count > 0:
                    temporary.InRegister = true;
                    temporary.Low = TakeInteger();
                    break;

                case X8664ValueClass.Fat when this.IntegerPool.Count > 1:
                    temporary.InRegister = true;
                    temporary.Low = TakeInteger();
                    temporary.High = TakeInteger();
                    break;

                case X8664ValueClass.Float32 or X8664ValueClass.Float64 when this.FloatPool.Count > 0:
                    temporary.InRegister = true;
                    temporary.Xmm = this.FloatPool[^1];
                    this.FloatPool.RemoveAt(this.FloatPool.Count - 1);
                    this.UsedFloats.Add(temporary.Xmm);
                    break;

                default:
                    temporary.Slot = this.FreeSlots.Count > 0 ? this.FreeSlots.Pop() : NewSlot();
                    break;
            }

            return temporary;
        }

        X8664Register TakeInteger()
        {
            X8664Register register = this.IntegerPool[^1];
            this.IntegerPool.RemoveAt(this.IntegerPool.Count - 1);
            this.UsedIntegers.Add(register);
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

            switch (temporary.Class)
            {
                case X8664ValueClass.Integer:
                    this.IntegerPool.Add(temporary.Low);
                    break;

                case X8664ValueClass.Fat:
                    this.IntegerPool.Add(temporary.High);
                    this.IntegerPool.Add(temporary.Low);
                    break;

                default:
                    this.FloatPool.Add(temporary.Xmm);
                    break;
            }
        }

        Temporary SaveTemporary(X8664ValueClass valueClass)
        {
            Temporary temporary = AllocateTemporary(valueClass);

            if (!temporary.InRegister)
            {
                StoreClass(valueClass, Slot(temporary.Slot));
                return temporary;
            }

            switch (valueClass)
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Mov(temporary.Low, X8664Register.RAX);
                    break;

                case X8664ValueClass.Fat:
                    this.Assembler.Mov(temporary.Low, X8664Register.RAX);
                    this.Assembler.Mov(temporary.High, X8664Register.RDX);
                    break;

                default:
                    this.Assembler.MovXmm(temporary.Xmm, X8664XmmRegister.XMM0);
                    break;
            }

            return temporary;
        }

        void LoadTemporary(Temporary temporary, bool secondary = false)
        {
            if (!temporary.InRegister)
            {
                LoadClass(temporary.Class, Slot(temporary.Slot), secondary);
                return;
            }

            switch (temporary.Class)
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Mov(secondary ? X8664Register.RCX : X8664Register.RAX, temporary.Low);
                    break;

                case X8664ValueClass.Fat:
                    this.Assembler.Mov(X8664Register.RAX, temporary.Low);
                    this.Assembler.Mov(X8664Register.RDX, temporary.High);
                    break;

                default:
                    this.Assembler.MovXmm(secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, temporary.Xmm);
                    break;
            }
        }

        X8664Register TemporaryRegister(Temporary temporary, X8664Register scratch, bool high = false)
        {
            if (temporary.InRegister)
            {
                return high ? temporary.High : temporary.Low;
            }

            this.Assembler.Load(scratch, Slot(temporary.Slot).Offset(high ? 8 : 0));
            return scratch;
        }

        X8664Memory GlobalMemory(IRGlobal global, X8664Register threadBlock)
        {
            if (!this.Context.ThreadLocals.TryGetValue(global, out int offset))
            {
                return X8664Memory.Rip(this.Context.GlobalSymbol(global));
            }

            this.Assembler.LoadThreadBlock(threadBlock, this.Context.ThreadIndex!);
            return X8664Memory.At(threadBlock, offset);
        }

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
                X8664ValueClass valueClass = X8664Layout.Classify(parameter.ValueType);

                if (valueClass == X8664ValueClass.Fat)
                {
                    MoveParameter(position++, X8664ValueClass.Integer, parameter, high: false);
                    MoveParameter(position++, X8664ValueClass.Integer, parameter, high: true);
                }
                else
                {
                    MoveParameter(position++, valueClass, parameter, high: false);
                }
            }
        }

        void MoveParameter(int position, X8664ValueClass valueClass, IRLocal parameter, bool high)
        {
            Home? home = RegisterHome(parameter);

            if (position >= 4)
            {
                X8664Memory source = X8664Memory.At(X8664Register.RBP, this.ArgumentBase + 8 * position);

                if (home != null)
                {
                    switch (valueClass)
                    {
                        case X8664ValueClass.Integer:
                            this.Assembler.Load(high ? home.High : home.Low, source);
                            break;

                        case X8664ValueClass.Float32:
                        case X8664ValueClass.Float64:
                            this.Assembler.LoadScalar(valueClass == X8664ValueClass.Float64, home.Xmm, source);
                            break;
                    }

                    return;
                }

                this.Assembler.Load(X8664Register.RAX, source);
                this.Assembler.Store(SlotOf(parameter).Offset(high ? 8 : 0), X8664Register.RAX);
                return;
            }

            switch (valueClass)
            {
                case X8664ValueClass.Integer:
                    if (home != null)
                    {
                        this.Assembler.Mov(high ? home.High : home.Low, IntegerArgumentRegisters[position]);
                    }
                    else
                    {
                        this.Assembler.Store(SlotOf(parameter).Offset(high ? 8 : 0), IntegerArgumentRegisters[position]);
                    }
                    break;

                case X8664ValueClass.Float32:
                case X8664ValueClass.Float64:
                    if (home != null)
                    {
                        this.Assembler.MovXmm(home.Xmm, FloatArgumentRegisters[position]);
                    }
                    else
                    {
                        this.Assembler.StoreScalar(valueClass == X8664ValueClass.Float64, SlotOf(parameter), FloatArgumentRegisters[position]);
                    }
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

        void LoadClass(X8664ValueClass valueClass, X8664Memory memory, bool secondary = false)
        {
            switch (valueClass)
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

        void StoreClass(X8664ValueClass valueClass, X8664Memory memory)
        {
            switch (valueClass)
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

        void LoadVariable(IRType type, X8664Memory memory, bool secondary = false) => LoadClass(X8664Layout.Classify(type), memory, secondary);

        void StoreVariable(IRType type, X8664Memory memory) => StoreClass(X8664Layout.Classify(type), memory);

        void LoadLocal(IRLocal local, bool secondary = false)
        {
            if (!this.Homes.TryGetValue(local, out Home? home))
            {
                LoadVariable(local.ValueType, SlotOf(local), secondary);
                return;
            }

            switch (home.Class)
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Mov(secondary ? X8664Register.RCX : X8664Register.RAX, home.Low);
                    break;

                case X8664ValueClass.Fat:
                    if (secondary)
                    {
                        throw ErrorHandler.Create("Fat pointers cannot be loaded into secondary registers.");
                    }

                    this.Assembler.Mov(X8664Register.RAX, home.Low);
                    this.Assembler.Mov(X8664Register.RDX, home.High);
                    break;

                default:
                    this.Assembler.MovXmm(secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, home.Xmm);
                    break;
            }
        }

        void StoreLocal(IRLocal local)
        {
            if (!this.Homes.TryGetValue(local, out Home? home))
            {
                StoreVariable(local.ValueType, SlotOf(local));
                return;
            }

            switch (home.Class)
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Mov(home.Low, X8664Register.RAX);
                    break;

                case X8664ValueClass.Fat:
                    this.Assembler.Mov(home.Low, X8664Register.RAX);
                    this.Assembler.Mov(home.High, X8664Register.RDX);
                    break;

                default:
                    this.Assembler.MovXmm(home.Xmm, X8664XmmRegister.XMM0);
                    break;
            }
        }

        void LoadInteger(IRValueInstruction value, X8664Register destination)
        {
            switch (value)
            {
                case IRLocal local when this.Homes.TryGetValue(local, out Home? home):
                    this.Assembler.Mov(destination, home.Low);
                    break;

                case IRLocal local:
                    this.Assembler.Load(destination, SlotOf(local));
                    break;

                case IRConstantInteger constant:
                    this.Assembler.MovImmediate(destination, X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value));
                    break;

                default:
                    throw ErrorHandler.Create($"'{value.Dump(0)}' is not a simple integer.");
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
                    if (memory.Uses(X8664Register.RAX))
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

                    if (store.Target is IRLocal stored && store.Offset == null)
                    {
                        this.KnownMultiples.Remove(stored);
                        this.NonNull.Remove(stored);

                        if (store.Value is IRLoadElement element && !this.ResultSlots.ContainsKey(element) && IRStructSchema.IsInline(element.ElementType))
                        {
                            this.NonNull.Add(stored);
                        }
                    }
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

                case IRFree free:
                    EmitValue(free.Pointer);
                    this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                    this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Free));
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
            if (IRSwitchAnalysis.Find(conditionalBranch, c => !this.ResultSlots.ContainsKey(c)) is IRSwitch jumpTable)
            {
                EmitJumpTable(jumpTable);
                return;
            }

            X8664Symbol elseLabel = NewLabel("else");
            X8664Symbol endLabel = NewLabel("endif");

            (IRLocal Local, long Multiple, bool InThen)? multiple = null;

            if (conditionalBranch.Condition is IRCompare { Right: IRConstantInteger { Value: 0 } } test && !this.ResultSlots.ContainsKey(test)
                && test.Operator is IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo && LowBitsMask(test.Left) is (IRLocal tested, long mask))
            {
                multiple = (tested, mask + 1, test.Operator == IRComparisonOperator.EqualTo);
            }

            EmitBranch(conditionalBranch.Condition, false, conditionalBranch.HasElseBlock ? elseLabel : endLabel);

            EmitBlockKnowing(conditionalBranch.ThenBlock, multiple is { InThen: true } known ? known : null);

            if (conditionalBranch.HasElseBlock)
            {
                if (!IRTree.AlwaysExits(conditionalBranch.ThenBlock))
                {
                    this.Assembler.Jump(endLabel);
                }

                this.Assembler.Bind(elseLabel);
                EmitBlockKnowing(conditionalBranch.ElseBlock, multiple is { InThen: false } knownElse ? knownElse : null);
            }

            this.Assembler.Bind(endLabel);
        }

        void EmitJumpTable(IRSwitch jumpTable)
        {
            X8664Symbol endLabel = NewLabel("switch.end");
            X8664Symbol defaultLabel = jumpTable.Default != null ? NewLabel("switch.default") : endLabel;
            X8664Symbol table = NewLabel("switch.table");
            List<X8664Symbol> caseLabels = jumpTable.Cases.Select(c => c != null ? NewLabel("switch.case") : endLabel).ToList();

            EmitValue(jumpTable.Subject);
            Normalize(jumpTable.Subject.ValueType);

            if (jumpTable.Minimum != 0)
            {
                this.Assembler.MovImmediate(X8664Register.RCX, jumpTable.Minimum);
                this.Assembler.Alu(X8664AluOperation.Subtract, X8664Register.RAX, X8664Register.RCX);
            }

            this.Assembler.AluImmediate(X8664AluOperation.Compare, X8664Register.RAX, jumpTable.Slots.Length - 1);
            this.Assembler.JumpIf(X8664Condition.Above, defaultLabel);
            this.Assembler.Lea(X8664Register.RCX, X8664Memory.Rip(table));
            this.Assembler.Load(4, true, X8664Register.RAX, X8664Memory.Indexed(X8664Register.RCX, X8664Register.RAX, 4));
            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.Jump(X8664Register.RAX);

            this.Assembler.Align(4);
            this.Assembler.Bind(table);

            foreach (int slot in jumpTable.Slots)
            {
                this.Assembler.TableEntry(slot < 0 ? defaultLabel : caseLabels[slot], table);
            }

            for (int i = 0; i < jumpTable.Cases.Count; i++)
            {
                if (jumpTable.Cases[i] is not IRBlock body)
                {
                    continue;
                }

                this.Assembler.Bind(caseLabels[i]);
                EmitBlock(body);

                if (!IRTree.AlwaysExits(body))
                {
                    this.Assembler.Jump(endLabel);
                }
            }

            if (jumpTable.Default != null)
            {
                this.Assembler.Bind(defaultLabel);
                EmitBlock(jumpTable.Default);
            }

            this.Assembler.Bind(endLabel);
        }

        void EmitBlockKnowing(IRBlock block, (IRLocal Local, long Multiple, bool InThen)? multiple)
        {
            if (multiple is not (IRLocal local, long value, _))
            {
                EmitBlock(block);
                return;
            }

            this.KnownMultiples[local] = value;
            EmitBlock(block);
            this.KnownMultiples.Remove(local);
        }

        void EmitLoop(IRLoop loop)
        {
            this.KnownMultiples.Clear();

            if (this.Context.OptimizeLoops && AnalyzeCountedLoop(loop) is CountedLoop counted)
            {
                EmitCountedLoop(loop, counted);
                return;
            }

            EmitLoopCore(loop);
        }

        void EmitLoopCore(IRLoop loop)
        {
            X8664Symbol topLabel = NewLabel("loop");
            X8664Symbol continueLabel = NewLabel("continue");
            X8664Symbol checkLabel = NewLabel("check");
            X8664Symbol exitLabel = NewLabel("exit");

            bool infinite = loop.Condition is IRConstantInteger { Value: not 0 };

            if (!infinite)
            {
                this.Assembler.Jump(checkLabel);
                this.Assembler.Align(16);
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

        void EmitBranch(IRValueInstruction condition, bool when, X8664Symbol target)
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

                    case IRCompare compare when X8664Layout.Classify(compare.OperandType) == X8664ValueClass.Integer:
                        EmitIntegerBranch(compare, when, target);
                        return;

                    case IRCompare compare when X8664Layout.Classify(compare.OperandType) is X8664ValueClass.Float32 or X8664ValueClass.Float64:
                        EmitFloatBranch(compare, when, target);
                        return;
                }
            }

            EmitValue(condition);
            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
            this.Assembler.JumpIf(when ? X8664Condition.NotEqual : X8664Condition.Equal, target);
        }

        void EmitIntegerBranch(IRCompare compare, bool when, X8664Symbol target)
        {
            X8664Condition code = IntegerCondition(compare);
            X8664Condition jump = when ? code : Invert(code);

            if (compare.Operator is IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo
                && compare.Right is IRConstantInteger { Value: 0 } && !this.ResultSlots.ContainsKey(compare.Right)
                && LowBitsMask(compare.Left) is (IRLocal dividend, long mask))
            {
                LoadInteger(dividend, X8664Register.RAX);
                this.Assembler.AluImmediate(X8664AluOperation.And, X8664Register.RAX, (int)mask);
                this.Assembler.JumpIf(jump, target);
                return;
            }

            if (!this.ResultSlots.ContainsKey(compare.Left) && RegisterHome(compare.Left) is Home { Class: X8664ValueClass.Integer } leftHome)
            {
                if (!this.ResultSlots.ContainsKey(compare.Right) && compare.Right is IRConstantInteger constant && FitsImmediate(constant, out int immediate))
                {
                    this.Assembler.AluImmediate(X8664AluOperation.Compare, leftHome.Low, immediate);
                }
                else if (!this.ResultSlots.ContainsKey(compare.Right) && RegisterHome(compare.Right) is Home { Class: X8664ValueClass.Integer } rightHome)
                {
                    this.Assembler.Alu(X8664AluOperation.Compare, leftHome.Low, rightHome.Low);
                }
                else
                {
                    EmitValue(compare.Right);
                    this.Assembler.Alu(X8664AluOperation.Compare, leftHome.Low, X8664Register.RAX);
                }

                this.Assembler.JumpIf(jump, target);
                return;
            }

            Operand right = EmitBinaryOperands(compare.Left, compare.Right, allowImmediate: true);
            Compare(right);
            this.Assembler.JumpIf(jump, target);
        }

        (IRLocal, long)? LowBitsMask(IRValueInstruction value)
        {
            if (this.ResultSlots.ContainsKey(value) || value is not IRSubtract { Left: IRLocal dividend } subtract
                || X8664Layout.Classify(dividend.ValueType) != X8664ValueClass.Integer || this.ResultSlots.ContainsKey(subtract.Right))
            {
                return null;
            }

            (IRValueInstruction? quotient, long power) = subtract.Right switch
            {
                IRMultiply { Left: var q, Right: IRConstantInteger c } => (q, (long)c.Value),
                IRBitwise { Operator: IRBitwiseOperator.ShiftLeft, Left: var q, Right: IRConstantInteger c } when c.Value is > 0 and < 31 => (q, 1L << (int)c.Value),
                _ => ((IRValueInstruction?)null, 0L),
            };

            if (quotient == null || this.ResultSlots.ContainsKey(quotient) || power < 2 || power > (1L << 30) || (power & (power - 1)) != 0)
            {
                return null;
            }

            bool sameDividend = quotient switch
            {
                IRDivide { Left: IRLocal divided, Right: IRConstantInteger divisor } => divided == dividend && divisor.Value == power,
                IRBitwise { Operator: IRBitwiseOperator.ShiftRight, Left: IRLocal shifted, Right: IRConstantInteger count } => shifted == dividend && !X8664Layout.IsSigned(dividend.ValueType) && (1L << (int)count.Value) == power,
                _ => false,
            };

            return sameDividend ? (dividend, power - 1) : null;
        }

        static X8664Condition Invert(X8664Condition condition) => (X8664Condition)((int)condition ^ 1);

        static X8664Condition IntegerCondition(IRCompare compare)
        {
            bool signed = X8664Layout.IsSigned(compare.OperandType);

            return compare.Operator switch
            {
                IRComparisonOperator.EqualTo => X8664Condition.Equal,
                IRComparisonOperator.NotEqualTo => X8664Condition.NotEqual,
                IRComparisonOperator.LessThan => signed ? X8664Condition.Less : X8664Condition.Below,
                IRComparisonOperator.LessThanOrEqual => signed ? X8664Condition.LessOrEqual : X8664Condition.BelowOrEqual,
                IRComparisonOperator.GreaterThan => signed ? X8664Condition.Greater : X8664Condition.Above,
                IRComparisonOperator.GreaterThanOrEqual => signed ? X8664Condition.GreaterOrEqual : X8664Condition.AboveOrEqual,
                _ => throw ErrorHandler.CreateNotImplimented($"Comparison '{compare.Operator}' is not implemented.")
            };
        }

        void Compare(Operand right)
        {
            if (right.Immediate is long immediate)
            {
                this.Assembler.AluImmediate(X8664AluOperation.Compare, X8664Register.RAX, (int)immediate);
            }
            else
            {
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, right.Register);
            }
        }

        void EmitFloatBranch(IRCompare compare, bool when, X8664Symbol target)
        {
            bool isDouble = X8664Layout.Classify(compare.OperandType) == X8664ValueClass.Float64;
            X8664XmmRegister right = EmitBinaryOperands(compare.Left, compare.Right, allowImmediate: false).Xmm;

            switch (compare.Operator)
            {
                case IRComparisonOperator.GreaterThan:
                    this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                    this.Assembler.JumpIf(when ? X8664Condition.Above : X8664Condition.BelowOrEqual, target);
                    break;

                case IRComparisonOperator.GreaterThanOrEqual:
                    this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                    this.Assembler.JumpIf(when ? X8664Condition.AboveOrEqual : X8664Condition.Below, target);
                    break;

                case IRComparisonOperator.LessThan:
                    this.Assembler.CompareScalar(isDouble, right, X8664XmmRegister.XMM0);
                    this.Assembler.JumpIf(when ? X8664Condition.Above : X8664Condition.BelowOrEqual, target);
                    break;

                case IRComparisonOperator.LessThanOrEqual:
                    this.Assembler.CompareScalar(isDouble, right, X8664XmmRegister.XMM0);
                    this.Assembler.JumpIf(when ? X8664Condition.AboveOrEqual : X8664Condition.Below, target);
                    break;

                case IRComparisonOperator.EqualTo:
                case IRComparisonOperator.NotEqualTo:
                    {
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);

                        if (when == (compare.Operator == IRComparisonOperator.EqualTo))
                        {
                            X8664Symbol skip = NewLabel("unordered");
                            this.Assembler.JumpIf(X8664Condition.Parity, skip);
                            this.Assembler.JumpIf(X8664Condition.Equal, target);
                            this.Assembler.Bind(skip);
                        }
                        else
                        {
                            this.Assembler.JumpIf(X8664Condition.Parity, target);
                            this.Assembler.JumpIf(X8664Condition.NotEqual, target);
                        }
                    }
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"Comparison '{compare.Operator}' is not implemented.");
            }
        }

        void EmitStore(IRStore store)
        {
            if (store.Offset == null)
            {
                switch (store.Target)
                {
                    case IRLocal local:
                        ExpectSameClass(local.ValueType, store.Value.ValueType, $"a store to '{local.Name}'");

                        if (TryEmitDirectStore(local, store.Value))
                        {
                            return;
                        }

                        if (!TryEmitAccumulate(local, store.Value))
                        {
                            EmitValue(store.Value);
                            StoreLocal(local);
                        }
                        return;

                    case IRGlobal global:
                        ExpectSameClass(global.ValueType, store.Value.ValueType, $"a store to '{global.Name}'");
                        EmitValue(store.Value);
                        StoreVariable(global.ValueType, GlobalMemory(global, X8664Register.R11));
                        return;
                }

                EmitValue(store.Target);
                Temporary address = SaveTemporary(X8664ValueClass.Integer);

                EmitValue(store.Value);
                X8664Register pointerRegister = TemporaryRegister(address, X8664Register.R11);
                Release(address);

                StoreTyped(store.Value.ValueType, X8664Memory.At(pointerRegister));
                return;
            }

            int displacement = 0;
            bool nullCheck = false;
            IRType type = store.Value.ValueType;
            IRValueInstruction? offsetValue = null;

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
                    offsetValue = store.Offset;
                    break;
            }

            X8664Register pointer;
            Temporary? pointerTemporary = null;
            Temporary? offsetTemporary = null;

            if (store.Target is IRLocal targetLocal && offsetValue == null)
            {
                EmitValue(store.Value);

                if (RegisterHome(targetLocal) is Home home)
                {
                    pointer = home.Low;
                }
                else
                {
                    this.Assembler.Load(X8664Register.R11, SlotOf(targetLocal));
                    pointer = X8664Register.R11;
                }
            }
            else
            {
                EmitValue(store.Target);
                pointerTemporary = SaveTemporary(X8664ValueClass.Integer);

                if (offsetValue != null)
                {
                    EmitValue(offsetValue);
                    offsetTemporary = SaveTemporary(X8664ValueClass.Integer);
                }

                EmitValue(store.Value);

                this.Assembler.Mov(X8664Register.R11, TemporaryRegister(pointerTemporary, X8664Register.R11));

                if (offsetTemporary != null)
                {
                    this.Assembler.Alu(X8664AluOperation.Add, X8664Register.R11, TemporaryRegister(offsetTemporary, X8664Register.R10));
                }

                pointer = X8664Register.R11;
            }

            if (nullCheck)
            {
                CheckNotNull(pointer, store.Target);
            }

            Release(offsetTemporary);
            Release(pointerTemporary);

            StoreTyped(type, X8664Memory.At(pointer, displacement));
        }

        bool TryEmitDirectStore(IRLocal local, IRValueInstruction value)
        {
            if (this.ResultSlots.ContainsKey(value) || RegisterHome(local) is not Home home)
            {
                return false;
            }

            switch (home.Class)
            {
                case X8664ValueClass.Integer when value is IRConstantInteger constant && X8664Layout.Classify(constant.ValueType) == X8664ValueClass.Integer:
                    this.Assembler.MovImmediate(home.Low, X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value));
                    return true;

                case X8664ValueClass.Integer when RegisterHome(value) is Home { Class: X8664ValueClass.Integer } source:
                    this.Assembler.Mov(home.Low, source.Low);
                    return true;

                case X8664ValueClass.Float32 or X8664ValueClass.Float64 when RegisterHome(value) is Home source && source.Class == home.Class:
                    this.Assembler.MovXmm(home.Xmm, source.Xmm);
                    return true;
            }

            return false;
        }

        bool TryEmitAccumulate(IRLocal local, IRValueInstruction value)
        {
            if (!this.Homes.TryGetValue(local, out Home? home) || home.Class == X8664ValueClass.Fat || value.ValueType.DataType != local.ValueType.DataType || this.ResultSlots.ContainsKey(value))
            {
                return false;
            }

            IRValueInstruction? left = null;
            IRValueInstruction? right = null;
            X8664SseOperation? sse = null;
            X8664AluOperation? alu = null;

            switch (value)
            {
                case IRAdd add:
                    (left, right, sse, alu) = (add.Left, add.Right, X8664SseOperation.Add, X8664AluOperation.Add);
                    break;

                case IRSubtract subtract:
                    (left, right, sse, alu) = (subtract.Left, subtract.Right, X8664SseOperation.Subtract, X8664AluOperation.Subtract);
                    break;

                case IRMultiply product:
                    (left, right, sse) = (product.Left, product.Right, X8664SseOperation.Multiply);
                    break;

                case IRDivide divide when home.Class != X8664ValueClass.Integer:
                    (left, right, sse) = (divide.Left, divide.Right, X8664SseOperation.Divide);
                    break;

                case IRBitwise { Operator: IRBitwiseOperator.And or IRBitwiseOperator.Or or IRBitwiseOperator.Xor } bitwise:
                    (left, right) = (bitwise.Left, bitwise.Right);
                    alu = bitwise.Operator switch
                    {
                        IRBitwiseOperator.And => X8664AluOperation.And,
                        IRBitwiseOperator.Or => X8664AluOperation.Or,
                        _ => X8664AluOperation.Xor
                    };
                    break;
            }

            if (left == null || right == null || left.ValueType.DataType != local.ValueType.DataType || right.ValueType.DataType != local.ValueType.DataType)
            {
                return false;
            }

            bool multiply = value is IRMultiply;

            if (home.Class == X8664ValueClass.Integer)
            {
                if (alu == null && !multiply)
                {
                    return false;
                }

                if (left != local)
                {
                    if (right != local || value is IRSubtract)
                    {
                        return false;
                    }

                    (left, right) = (right, left);
                }

                if (right is IRConstantInteger constant && FitsImmediate(constant, out int immediate))
                {
                    if (multiply)
                    {
                        this.Assembler.ImulImmediate(home.Low, home.Low, immediate);
                    }
                    else
                    {
                        this.Assembler.AluImmediate(alu!.Value, home.Low, immediate);
                    }
                }
                else
                {
                    X8664Register source;

                    if (RegisterHome(right) is { Class: X8664ValueClass.Integer } rightHome)
                    {
                        source = rightHome.Low;
                    }
                    else
                    {
                        EmitValue(right);
                        source = X8664Register.RAX;
                    }

                    if (multiply)
                    {
                        this.Assembler.Imul(home.Low, source);
                    }
                    else
                    {
                        this.Assembler.Alu(alu!.Value, home.Low, source);
                    }
                }

                Normalize(local.ValueType, home.Low);
                return true;
            }

            if (sse == null || left != local)
            {
                return false;
            }

            X8664XmmRegister operand;

            if (RegisterHome(right) is { Class: X8664ValueClass.Float32 or X8664ValueClass.Float64 } floatHome)
            {
                operand = floatHome.Xmm;
            }
            else
            {
                EmitValue(right);
                operand = X8664XmmRegister.XMM0;
            }

            this.Assembler.ScalarArithmetic(sse.Value, home.Class == X8664ValueClass.Float64, home.Xmm, operand);
            return true;
        }

        static bool FitsImmediate(IRConstantInteger constant, out int immediate)
        {
            immediate = 0;

            if (X8664Layout.Classify(constant.ValueType) != X8664ValueClass.Integer)
            {
                return false;
            }

            long value = X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value);

            if (value < int.MinValue || value > int.MaxValue)
            {
                return false;
            }

            immediate = (int)value;
            return true;
        }

        FatSource ArraySource(IRValueInstruction target)
        {
            if (target is IRLocal local)
            {
                if (RegisterHome(local) is Home home)
                {
                    return new FatSource { InRegisters = true, Pointer = home.Low, Length = home.High };
                }

                return new FatSource { Memory = SlotOf(local) };
            }

            EmitValue(target);
            Temporary temporary = SaveTemporary(X8664ValueClass.Fat);

            return temporary.InRegister
                ? new FatSource { InRegisters = true, Pointer = temporary.Low, Length = temporary.High, Temporary = temporary }
                : new FatSource { Memory = Slot(temporary.Slot), Temporary = temporary };
        }

        void CheckIndex(FatSource source, X8664Register index)
        {
            if (source.InRegisters)
            {
                this.Assembler.Alu(X8664AluOperation.Compare, index, source.Length);
            }
            else
            {
                this.Assembler.Alu(X8664AluOperation.Compare, index, source.Memory.Offset(8));
            }

            this.Assembler.JumpIf(X8664Condition.AboveOrEqual, PanicStub(IndexOutOfRangeMessage));
        }

        X8664Register PointerOf(FatSource source, X8664Register scratch)
        {
            if (source.InRegisters)
            {
                return source.Pointer;
            }

            this.Assembler.Load(scratch, source.Memory);
            return scratch;
        }

        X8664Memory ElementAddress(X8664Register pointer, X8664Register index, int width)
        {
            if (width is 1 or 2 or 4 or 8)
            {
                return X8664Memory.Indexed(pointer, index, width);
            }

            if ((width & (width - 1)) == 0)
            {
                this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftLeft, index, System.Numerics.BitOperations.Log2((uint)width));
            }
            else
            {
                this.Assembler.ImulImmediate(index, index, width);
            }

            return X8664Memory.Indexed(pointer, index, 1);
        }

        void StoreElementValue(IRType elementType, X8664Memory address)
        {
            if (!IRStructSchema.IsInline(elementType))
            {
                StoreTyped(elementType, address);
                return;
            }

            this.Assembler.Lea(X8664Register.R11, address);
            CopyStruct((IRStructSchema)elementType.UserObject!, X8664Register.R11, 0);
        }

        void CopyStruct(IRStructSchema schema, X8664Register destination, int offset)
        {
            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
            this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(NullReferenceMessage));

            for (int i = 0; i < X8664Layout.SizeOf(schema); i += 8)
            {
                this.Assembler.Load(X8664Register.RCX, X8664Memory.At(X8664Register.RAX, i));
                this.Assembler.Store(X8664Memory.At(destination, offset + i), X8664Register.RCX);
            }
        }

        static bool IsSimpleIndex(IRValueInstruction index) => index is IRLocal || index is IRConstantInteger constant && X8664Layout.Classify(constant.ValueType) == X8664ValueClass.Integer;

        static bool SameIndex(IRValueInstruction a, IRValueInstruction b)
        {
            if (a == b)
            {
                return true;
            }

            return a is IRConstantInteger left && b is IRConstantInteger right && left.IntegerType == right.IntegerType && left.Value == right.Value;
        }

        IRLoadElement? CompoundLoad(IRValueInstruction value, IRLocal target, IRValueInstruction index)
        {
            while (true)
            {
                if (this.ResultSlots.ContainsKey(value))
                {
                    return null;
                }

                switch (value)
                {
                    case IRConvert convert:
                        value = convert.Value;
                        break;

                    case IRAdd add:
                        value = add.Left;
                        break;

                    case IRSubtract subtract:
                        value = subtract.Left;
                        break;

                    case IRMultiply multiply:
                        value = multiply.Left;
                        break;

                    case IRDivide divide:
                        value = divide.Left;
                        break;

                    case IRBitwise bitwise:
                        value = bitwise.Left;
                        break;

                    case IRLoadElement load when load.Target == target && SameIndex(load.Index, index):
                        return load;

                    default:
                        return null;
                }
            }
        }

        void EmitStoreElement(IRStoreElement store)
        {
            ExpectSameClass(store.ElementType, store.Value.ValueType, "an array element store");
            ExpectFat(store.Target);

            int width = X8664Layout.ElementSize(store.ElementType);

            bool skipCheck = this.Unchecked.Contains(store);

            if (store.Target is IRLocal targetLocal && IsSimpleIndex(store.Index))
            {
                FatSource local = ArraySource(targetLocal);
                IRLoadElement? compound = skipCheck ? null : CompoundLoad(store.Value, targetLocal, store.Index);

                if (compound != null)
                {
                    LoadInteger(store.Index, X8664Register.R11);
                    CheckIndex(local, X8664Register.R11);
                    this.CheckedLoads.Add(compound);
                }

                EmitValue(store.Value);

                if (compound != null)
                {
                    this.CheckedLoads.Remove(compound);
                }

                LoadInteger(store.Index, X8664Register.R11);

                if (compound == null && !skipCheck)
                {
                    CheckIndex(local, X8664Register.R11);
                }

                StoreElementValue(store.ElementType, ElementAddress(PointerOf(local, X8664Register.R10), X8664Register.R11, width));
                return;
            }

            FatSource array = ArraySource(store.Target);
            Temporary? index = null;

            if (!IsSimpleIndex(store.Index))
            {
                EmitValue(store.Index);
                index = SaveTemporary(X8664ValueClass.Integer);
            }

            EmitValue(store.Value);

            if (index != null)
            {
                this.Assembler.Mov(X8664Register.R11, TemporaryRegister(index, X8664Register.R11));
            }
            else
            {
                LoadInteger(store.Index, X8664Register.R11);
            }

            if (!skipCheck)
            {
                CheckIndex(array, X8664Register.R11);
            }

            StoreElementValue(store.ElementType, ElementAddress(PointerOf(array, X8664Register.R10), X8664Register.R11, width));

            Release(index);
            Release(array.Temporary);
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
                    LoadLocal(local, secondary: true);
                    break;

                case IRGlobal global:
                    LoadVariable(global.ValueType, GlobalMemory(global, X8664Register.RCX), secondary: true);
                    break;

                case IRStructProperty property:
                    this.Assembler.MovImmediate(X8664Register.RCX, X8664Layout.OffsetOf(property));
                    break;
            }
        }

        Operand Secondary() => new Operand { Register = X8664Register.RCX, Xmm = X8664XmmRegister.XMM1 };

        (IRStructProperty, IRLocal)? FieldLoad(IRValueInstruction value)
        {
            if (value is IRLoad { Offset: IRStructProperty property, Target: IRLocal pointer } && !this.ResultSlots.ContainsKey(value) && !this.ResultSlots.ContainsKey(pointer)
                && X8664Layout.Classify(pointer.ValueType) == X8664ValueClass.Integer)
            {
                return (property, pointer);
            }

            return null;
        }

        bool WritesNothing(IRValueInstruction value)
        {
            if (this.ResultSlots.ContainsKey(value))
            {
                return true;
            }

            if (value is IRCall or IRCallIndirect or IRDispatch or IRAwait or IRBytes or IRMalloc or IRInstantiateArray or IRInstantiateStruct)
            {
                return false;
            }

            return IRTree.Children(value).All(WritesNothing);
        }

        void LoadSecondary(IRType type, X8664Memory memory)
        {
            switch (X8664Layout.Classify(type))
            {
                case X8664ValueClass.Integer:
                    this.Assembler.Load(X8664Layout.SizeOf(type), X8664Layout.IsSigned(type), X8664Register.RCX, memory);
                    break;

                case X8664ValueClass.Float32:
                    this.Assembler.LoadScalar(false, X8664XmmRegister.XMM1, memory);
                    break;

                default:
                    this.Assembler.LoadScalar(true, X8664XmmRegister.XMM1, memory);
                    break;
            }
        }

        Operand EmitBinaryOperands(IRValueInstruction left, IRValueInstruction right, bool allowImmediate)
        {
            if (!this.ResultSlots.ContainsKey(right))
            {
                if (allowImmediate && right is IRConstantInteger constant && FitsImmediate(constant, out int immediate))
                {
                    EmitValue(left);
                    return new Operand { Immediate = immediate };
                }

                if (RegisterHome(right) is Home home && home.Class != X8664ValueClass.Fat)
                {
                    EmitValue(left);
                    return new Operand { Register = home.Low, Xmm = home.Xmm };
                }
            }

            if (FieldLoad(right) is (IRStructProperty rightField, IRLocal rightPointer) && X8664Layout.Classify(right.ValueType) is X8664ValueClass.Integer or X8664ValueClass.Float32 or X8664ValueClass.Float64)
            {
                EmitValue(left);

                X8664Register pointer = X8664Register.R11;

                if (RegisterHome(rightPointer) is Home { Class: X8664ValueClass.Integer } pointerHome)
                {
                    pointer = pointerHome.Low;
                }
                else
                {
                    this.Assembler.Load(X8664Register.R11, SlotOf(rightPointer));
                }

                CheckNotNull(pointer, rightPointer);
                LoadSecondary(right.ValueType, X8664Memory.At(pointer, X8664Layout.OffsetOf(rightField)));
                return Secondary();
            }

            if (IsSimple(right))
            {
                EmitValue(left);
                EmitSimpleSecondary(right);
                return Secondary();
            }

            if (FieldLoad(left) is (_, IRLocal leftPointer) && this.NonNull.Contains(leftPointer) && WritesNothing(right)
                && X8664Layout.Classify(left.ValueType) is X8664ValueClass.Integer or X8664ValueClass.Float32 or X8664ValueClass.Float64)
            {
                EmitValue(right);
                MoveToSecondary(right.ValueType);
                EmitValue(left);
                return Secondary();
            }

            if (!this.ResultSlots.ContainsKey(left) && left is IRLocal or IRConstantInteger && X8664Layout.Classify(left.ValueType) != X8664ValueClass.Fat)
            {
                EmitValue(right);
                MoveToSecondary(right.ValueType);
                EmitValue(left);
                return Secondary();
            }

            EmitValue(left);
            Temporary temporary = SaveTemporary(X8664Layout.Classify(left.ValueType));

            EmitValue(right);
            MoveToSecondary(right.ValueType);

            LoadTemporary(temporary);
            Release(temporary);
            return Secondary();
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
                    LoadLocal(local);
                    break;

                case IRGlobal global:
                    LoadVariable(global.ValueType, GlobalMemory(global, X8664Register.RAX));
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

                case IRMath math:
                    EmitMath(math);
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

                    if (RegisterHome(length.Target) is Home home)
                    {
                        this.Assembler.Mov(X8664Register.RAX, home.High);
                    }
                    else if (length.Target is IRLocal lengthLocal)
                    {
                        this.Assembler.Load(X8664Register.RAX, SlotOf(lengthLocal).Offset(8));
                    }
                    else
                    {
                        EmitValue(length.Target);
                        this.Assembler.Mov(X8664Register.RAX, X8664Register.RDX);
                    }

                    Normalize(length.ValueType);
                    break;

                case IRFatPointer fatPointer:
                    {
                        EmitValue(fatPointer.Length);
                        Temporary length = SaveTemporary(X8664ValueClass.Integer);

                        EmitValue(fatPointer.Pointer);
                        this.Assembler.Mov(X8664Register.RDX, TemporaryRegister(length, X8664Register.RDX));
                        Release(length);
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
                    this.Assembler.LoadScalar(false, secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, X8664Memory.Rip(this.Context.FloatConstant((uint)BitConverter.SingleToInt32Bits((float)constant.Value))));
                    break;

                case IRDataTypes.Float64:
                    this.Assembler.LoadScalar(true, secondary ? X8664XmmRegister.XMM1 : X8664XmmRegister.XMM0, X8664Memory.Rip(this.Context.FloatConstant(BitConverter.DoubleToInt64Bits(constant.Value))));
                    break;

                default:
                    this.Assembler.MovImmediate(secondary ? X8664Register.RCX : X8664Register.RAX, X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value));
                    break;
            }
        }

        void EmitMath(IRMath math)
        {
            X8664ValueClass valueClass = X8664Layout.Classify(math.ValueType);

            if (math.Operation == IRMathOperation.Reinterpret)
            {
                X8664ValueClass sourceClass = X8664Layout.Classify(math.Value.ValueType);
                EmitValue(math.Value);

                switch ((sourceClass, valueClass))
                {
                    case (X8664ValueClass.Float32 or X8664ValueClass.Float64, X8664ValueClass.Integer):
                        this.Assembler.MovFromXmm(X8664Register.RAX, X8664XmmRegister.XMM0, sourceClass == X8664ValueClass.Float64);
                        return;

                    case (X8664ValueClass.Integer, X8664ValueClass.Float32 or X8664ValueClass.Float64):
                        this.Assembler.MovToXmm(X8664XmmRegister.XMM0, X8664Register.RAX, valueClass == X8664ValueClass.Float64);
                        return;

                    default:
                        throw ErrorHandler.Create($"Cannot reinterpret {math.Value.ValueType.Dump(0)} as {math.ValueType.Dump(0)}.");
                }
            }

            if (valueClass is not (X8664ValueClass.Float32 or X8664ValueClass.Float64))
            {
                throw ErrorHandler.Create($"'{math.Operation}' is not supported on values of type '{math.ValueType.Dump(0)}'.");
            }

            EmitValue(math.Value);

            switch (math.Operation)
            {
                case IRMathOperation.SquareRoot:
                    this.Assembler.ScalarArithmetic(X8664SseOperation.SquareRoot, valueClass == X8664ValueClass.Float64, X8664XmmRegister.XMM0, X8664XmmRegister.XMM0);
                    break;

                default:
                    throw ErrorHandler.CreateNotImplimented($"'{math.Operation}' is not implemented when targeting x86-64.");
            }
        }

        void EmitArithmetic(X8664SseOperation operation, IRValueInstruction left, IRValueInstruction right, IRType type)
        {
            X8664ValueClass valueClass = X8664Layout.Classify(type);

            switch (valueClass)
            {
                case X8664ValueClass.Float32:
                case X8664ValueClass.Float64:
                    {
                        Operand operand = EmitBinaryOperands(left, right, allowImmediate: false);
                        this.Assembler.ScalarArithmetic(operation, valueClass == X8664ValueClass.Float64, X8664XmmRegister.XMM0, operand.Xmm);
                    }
                    return;

                case X8664ValueClass.Integer:
                    break;

                default:
                    throw ErrorHandler.Create($"Arithmetic is not supported on values of type '{type.Dump(0)}'.");
            }

            if (operation == X8664SseOperation.Multiply && TryEmitMultiplyImmediate(left, right) || TryEmitExactDivide(operation, left, right, type))
            {
                Normalize(type);
                return;
            }

            Operand right64 = EmitBinaryOperands(left, right, allowImmediate: true);

            switch (operation)
            {
                case X8664SseOperation.Add:
                    if (right64.Immediate is long add)
                    {
                        this.Assembler.AluImmediate(X8664AluOperation.Add, X8664Register.RAX, (int)add);
                    }
                    else
                    {
                        this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, right64.Register);
                    }
                    break;

                case X8664SseOperation.Subtract:
                    if (right64.Immediate is long subtract)
                    {
                        this.Assembler.AluImmediate(X8664AluOperation.Subtract, X8664Register.RAX, (int)subtract);
                    }
                    else
                    {
                        this.Assembler.Alu(X8664AluOperation.Subtract, X8664Register.RAX, right64.Register);
                    }
                    break;

                case X8664SseOperation.Multiply:
                    if (right64.Immediate is long multiply)
                    {
                        this.Assembler.ImulImmediate(X8664Register.RAX, X8664Register.RAX, (int)multiply);
                    }
                    else
                    {
                        this.Assembler.Imul(X8664Register.RAX, right64.Register);
                    }
                    break;

                case X8664SseOperation.Divide when right64.Immediate is long power && power > 1 && (power & (power - 1)) == 0:
                    {
                        int shift = System.Numerics.BitOperations.Log2((ulong)power);

                        if (X8664Layout.IsSigned(type))
                        {
                            this.Assembler.Mov(X8664Register.RCX, X8664Register.RAX);
                            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightArithmetic, X8664Register.RCX, 63);
                            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightLogical, X8664Register.RCX, 64 - shift);
                            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, X8664Register.RCX);
                            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightArithmetic, X8664Register.RAX, shift);
                        }
                        else
                        {
                            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightLogical, X8664Register.RAX, shift);
                        }
                    }
                    break;

                case X8664SseOperation.Divide:
                    {
                        X8664Register divisor;
                        bool knownNonZero = false;
                        bool knownNotMinusOne = false;

                        if (right64.Immediate is long immediate)
                        {
                            this.Assembler.MovImmediate(X8664Register.RCX, immediate);
                            divisor = X8664Register.RCX;
                            knownNonZero = immediate != 0;
                            knownNotMinusOne = immediate != -1;
                        }
                        else
                        {
                            divisor = right64.Register;
                        }

                        if (!knownNonZero)
                        {
                            this.Assembler.Test(divisor, divisor);
                            this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(DivideByZeroMessage));
                        }

                        if (X8664Layout.IsSigned(type))
                        {
                            if (knownNotMinusOne)
                            {
                                this.Assembler.Cqo();
                                this.Assembler.Divide(true, divisor);
                            }
                            else
                            {
                                X8664Symbol divide = NewLabel("divide");
                                X8664Symbol done = NewLabel("divided");

                                this.Assembler.AluImmediate(X8664AluOperation.Compare, divisor, -1);
                                this.Assembler.JumpIf(X8664Condition.NotEqual, divide);
                                this.Assembler.Negate(X8664Register.RAX);
                                this.Assembler.Jump(done);

                                this.Assembler.Bind(divide);
                                this.Assembler.Cqo();
                                this.Assembler.Divide(true, divisor);
                                this.Assembler.Bind(done);
                            }
                        }
                        else
                        {
                            this.Assembler.Alu(X8664AluOperation.Xor, X8664Register.RDX, X8664Register.RDX);
                            this.Assembler.Divide(false, divisor);
                        }
                    }
                    break;
            }

            Normalize(type);
        }

        bool TryEmitMultiplyImmediate(IRValueInstruction left, IRValueInstruction right)
        {
            foreach ((IRValueInstruction factor, IRValueInstruction other) in new[] { (left, right), (right, left) })
            {
                if (!this.ResultSlots.ContainsKey(factor) && !this.ResultSlots.ContainsKey(other) && factor is IRConstantInteger constant
                    && FitsImmediate(constant, out int immediate) && RegisterHome(other) is Home { Class: X8664ValueClass.Integer } home)
                {
                    this.Assembler.ImulImmediate(X8664Register.RAX, home.Low, immediate);
                    return true;
                }
            }

            return false;
        }

        bool TryEmitExactDivide(X8664SseOperation operation, IRValueInstruction left, IRValueInstruction right, IRType type)
        {
            if (operation != X8664SseOperation.Divide || !X8664Layout.IsSigned(type) || left is not IRLocal local || this.ResultSlots.ContainsKey(left)
                || this.ResultSlots.ContainsKey(right) || right is not IRConstantInteger { Value: > 1 and <= (1L << 30) } divisor
                || !this.KnownMultiples.TryGetValue(local, out long multiple))
            {
                return false;
            }

            long power = (long)divisor.Value;

            if ((power & (power - 1)) != 0 || multiple % power != 0)
            {
                return false;
            }

            LoadInteger(local, X8664Register.RAX);
            this.Assembler.ShiftImmediate(X8664ShiftOperation.ShiftRightArithmetic, X8664Register.RAX, System.Numerics.BitOperations.Log2((ulong)power));
            return true;
        }

        void EmitBitwise(IRBitwise bitwise)
        {
            if (X8664Layout.Classify(bitwise.ValueType) != X8664ValueClass.Integer)
            {
                throw ErrorHandler.Create($"Bitwise operations are not supported on values of type '{bitwise.ValueType.Dump(0)}'.");
            }

            Operand right = EmitBinaryOperands(bitwise.Left, bitwise.Right, allowImmediate: true);

            X8664AluOperation? alu = bitwise.Operator switch
            {
                IRBitwiseOperator.And => X8664AluOperation.And,
                IRBitwiseOperator.Or => X8664AluOperation.Or,
                IRBitwiseOperator.Xor => X8664AluOperation.Xor,
                _ => null
            };

            if (alu != null)
            {
                if (right.Immediate is long immediate)
                {
                    this.Assembler.AluImmediate(alu.Value, X8664Register.RAX, (int)immediate);
                }
                else
                {
                    this.Assembler.Alu(alu.Value, X8664Register.RAX, right.Register);
                }
            }
            else
            {
                X8664ShiftOperation shift = bitwise.Operator == IRBitwiseOperator.ShiftLeft
                    ? X8664ShiftOperation.ShiftLeft
                    : X8664Layout.IsSigned(bitwise.ValueType) ? X8664ShiftOperation.ShiftRightArithmetic : X8664ShiftOperation.ShiftRightLogical;

                int mask = X8664Layout.SizeOf(bitwise.ValueType) <= 4 ? 31 : 63;

                if (right.Immediate is long count)
                {
                    this.Assembler.ShiftImmediate(shift, X8664Register.RAX, (int)(count & mask));
                }
                else
                {
                    this.Assembler.Mov(X8664Register.RCX, right.Register);

                    if (mask == 31)
                    {
                        this.Assembler.AluImmediate(X8664AluOperation.And, X8664Register.RCX, 31);
                    }

                    this.Assembler.Shift(shift, X8664Register.RAX);
                }
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

            if (valueClass is X8664ValueClass.Float32 or X8664ValueClass.Float64)
            {
                X8664XmmRegister right = EmitBinaryOperands(compare.Left, compare.Right, allowImmediate: false).Xmm;
                bool isDouble = valueClass == X8664ValueClass.Float64;

                switch (compare.Operator)
                {
                    case IRComparisonOperator.GreaterThan:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                        this.Assembler.SetCondition(X8664Condition.Above, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.GreaterThanOrEqual:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                        this.Assembler.SetCondition(X8664Condition.AboveOrEqual, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.LessThan:
                        this.Assembler.CompareScalar(isDouble, right, X8664XmmRegister.XMM0);
                        this.Assembler.SetCondition(X8664Condition.Above, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.LessThanOrEqual:
                        this.Assembler.CompareScalar(isDouble, right, X8664XmmRegister.XMM0);
                        this.Assembler.SetCondition(X8664Condition.AboveOrEqual, X8664Register.RAX);
                        break;

                    case IRComparisonOperator.EqualTo:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                        this.Assembler.SetCondition(X8664Condition.Equal, X8664Register.RAX);
                        this.Assembler.SetCondition(X8664Condition.NoParity, X8664Register.RCX);
                        this.Assembler.Alu(X8664AluOperation.And, X8664Register.RAX, X8664Register.RCX);
                        break;

                    case IRComparisonOperator.NotEqualTo:
                        this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, right);
                        this.Assembler.SetCondition(X8664Condition.NotEqual, X8664Register.RAX);
                        this.Assembler.SetCondition(X8664Condition.Parity, X8664Register.RCX);
                        this.Assembler.Alu(X8664AluOperation.Or, X8664Register.RAX, X8664Register.RCX);
                        break;
                }

                this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
                return;
            }

            Operand operand = EmitBinaryOperands(compare.Left, compare.Right, allowImmediate: true);
            Compare(operand);
            this.Assembler.SetCondition(IntegerCondition(compare), X8664Register.RAX);
            this.Assembler.MovZeroExtend8(X8664Register.RAX, X8664Register.RAX);
        }

        void EmitFatCompare(IRCompare compare)
        {
            if (compare.Operator is not (IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo))
            {
                throw ErrorHandler.Create($"Only equality comparisons are supported on values of type '{compare.OperandType.Dump(0)}'.");
            }

            EmitValue(compare.Left);
            Temporary temporary = SaveTemporary(X8664ValueClass.Fat);

            EmitValue(compare.Right);
            this.Assembler.Mov(X8664Register.R8, TemporaryRegister(temporary, X8664Register.R8, high: true));
            this.Assembler.Mov(X8664Register.RCX, TemporaryRegister(temporary, X8664Register.RCX));
            Release(temporary);

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
                        else if (target.DataType == IRDataTypes.UInt64)
                        {
                            X8664Symbol large = NewLabel("convert.large");
                            X8664Symbol done = NewLabel("convert.done");
                            long half = isDouble ? BitConverter.DoubleToInt64Bits(9223372036854775808.0) : (uint)BitConverter.SingleToInt32Bits(9223372036854775808.0f);

                            this.Assembler.LoadScalar(isDouble, X8664XmmRegister.XMM1, X8664Memory.Rip(this.Context.FloatConstant(half)));
                            this.Assembler.CompareScalar(isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                            this.Assembler.JumpIf(X8664Condition.AboveOrEqual, large);
                            this.Assembler.ConvertScalarToInteger(isDouble, X8664Register.RAX, X8664XmmRegister.XMM0);
                            this.Assembler.Jump(done);

                            this.Assembler.Bind(large);
                            this.Assembler.ScalarArithmetic(X8664SseOperation.Subtract, isDouble, X8664XmmRegister.XMM0, X8664XmmRegister.XMM1);
                            this.Assembler.ConvertScalarToInteger(isDouble, X8664Register.RAX, X8664XmmRegister.XMM0);
                            this.Assembler.MovImmediate(X8664Register.RCX, long.MinValue);
                            this.Assembler.Alu(X8664AluOperation.Xor, X8664Register.RAX, X8664Register.RCX);

                            this.Assembler.Bind(done);
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
                        LoadLocal(local);
                        return;

                    case IRGlobal global:
                        LoadVariable(load.ValueType, GlobalMemory(global, X8664Register.RAX));
                        return;
                }

                EmitValue(load.Target);
                LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX));
                return;
            }

            X8664Register pointer = X8664Register.RAX;

            if (load.Offset is IRStructProperty or IRConstantInteger && RegisterHome(load.Target) is { Class: X8664ValueClass.Integer } home)
            {
                pointer = home.Low;
            }
            else
            {
                EmitValue(load.Target);
            }

            switch (load.Offset)
            {
                case IRStructProperty property:
                    CheckNotNull(pointer, load.Target);
                    LoadTyped(load.ValueType, X8664Memory.At(pointer, X8664Layout.OffsetOf(property)));
                    return;

                case IRConstantInteger constant:
                    LoadTyped(load.ValueType, X8664Memory.At(pointer, (int)constant.Value));
                    return;
            }

            Temporary pointerTemporary = SaveTemporary(X8664ValueClass.Integer);

            EmitValue(load.Offset);
            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, TemporaryRegister(pointerTemporary, X8664Register.RCX));
            Release(pointerTemporary);

            LoadTyped(load.ValueType, X8664Memory.At(X8664Register.RAX));
        }

        void EmitLoadElement(IRLoadElement load)
        {
            ExpectFat(load.Target);

            int width = X8664Layout.ElementSize(load.ElementType);

            FatSource array = ArraySource(load.Target);

            EmitValue(load.Index);

            if (!this.CheckedLoads.Contains(load) && !this.Unchecked.Contains(load))
            {
                CheckIndex(array, X8664Register.RAX);
            }

            X8664Memory address = ElementAddress(PointerOf(array, X8664Register.R11), X8664Register.RAX, width);

            if (IRStructSchema.IsInline(load.ElementType))
            {
                this.Assembler.Lea(X8664Register.RAX, address);
            }
            else
            {
                LoadTyped(load.ElementType, address);
            }

            Release(array.Temporary);
        }

        void CheckNotNull(X8664Register pointer, IRValueInstruction source)
        {
            IRLocal? local = source is IRLocal candidate && !this.ResultSlots.ContainsKey(source) ? candidate : null;

            if (local != null && this.NonNull.Contains(local))
            {
                return;
            }

            this.Assembler.Test(pointer, pointer);
            this.Assembler.JumpIf(X8664Condition.Equal, PanicStub(NullReferenceMessage));

            if (local != null)
            {
                this.NonNull.Add(local);
            }
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

            Temporary pointer = SaveTemporary(X8664ValueClass.Integer);

            for (int i = 0; i < instantiate.PropertyValues.Count; i++)
            {
                IRStructProperty property = instantiate.StructSchema.Properties[i];
                IRValueInstruction propertyValue = instantiate.PropertyValues[i];

                ExpectSameClass(property.ValueType, propertyValue.ValueType, $"the initialization of '{property.Name}'");

                EmitValue(propertyValue);
                StoreTyped(property.ValueType, X8664Memory.At(TemporaryRegister(pointer, X8664Register.R11), X8664Layout.OffsetOf(property)));
            }

            LoadTemporary(pointer);
            Release(pointer);
        }

        void EmitInstantiateArray(IRInstantiateArray instantiate)
        {
            IRType elementType = instantiate.ArraySchema.ElementType;
            int width = X8664Layout.ElementSize(elementType);

            EmitValue(instantiate.Size);
            this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
            this.Assembler.JumpIf(X8664Condition.Sign, PanicStub(NegativeSizeMessage));

            Temporary length = SaveTemporary(X8664ValueClass.Integer);

            this.Assembler.ImulImmediate(X8664Register.RCX, X8664Register.RAX, width);
            CallMalloc();

            Temporary pointer = SaveTemporary(X8664ValueClass.Integer);

            for (int i = 0; i < instantiate.ElementValues.Count; i++)
            {
                ExpectSameClass(elementType, instantiate.ElementValues[i].ValueType, "an array initializer");

                EmitValue(instantiate.ElementValues[i]);

                if (IRStructSchema.IsInline(elementType))
                {
                    CopyStruct((IRStructSchema)elementType.UserObject!, TemporaryRegister(pointer, X8664Register.R11), i * width);
                }
                else
                {
                    StoreTyped(elementType, X8664Memory.At(TemporaryRegister(pointer, X8664Register.R11), i * width));
                }
            }

            this.Assembler.Mov(X8664Register.RAX, TemporaryRegister(pointer, X8664Register.RAX));
            this.Assembler.Mov(X8664Register.RDX, TemporaryRegister(length, X8664Register.RDX));

            Release(pointer);
            Release(length);
        }

        void EmitInvocation(IRValueInstruction invocation)
        {
            switch (invocation)
            {
                case IRCall call when call.Function is IRFunctionImport functionImport && this.Context.RuntimeImports.TryGetValue(functionImport, out IRFunction? runtimeFunction):
                    EmitCall(call.Arguments, false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(runtimeFunction)), call.ValueType);
                    break;

                case IRCall call when call.Function is IRFunctionImport functionImport:
                    EmitCall(call.Arguments, true, null, () => this.Assembler.Call(X8664Memory.Rip(this.Context.ImportSymbol(functionImport))), call.ValueType);
                    break;

                case IRCall call:
                    EmitCall(call.Arguments, false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(call.Function)), call.ValueType);
                    break;

                case IRCallIndirect indirect:
                    EmitCall(indirect.Arguments, false, indirect.FunctionPointer, null, indirect.ValueType);
                    break;

                case IRDispatch dispatch:
                    {
                        if (!this.Context.Dispatchers.TryGetValue(dispatch.Kernel, out IRFunction? dispatcher))
                        {
                            throw ErrorHandler.Create($"No dispatcher was generated for kernel '{dispatch.Kernel.Name}'.");
                        }

                        List<IRValueInstruction> residents = dispatch.Residents.Count > 0
                            ? dispatch.Residents
                            : dispatch.Kernel.KernelParameters.Where(p => p.ValueType.DataType == IRDataTypes.Array).Select(_ => (IRValueInstruction)new IRConstantInteger(IRDataTypes.Pointer, 0)).ToList();

                        EmitCall([dispatch.Count, .. dispatch.Arguments, .. residents], false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(dispatcher)), dispatch.ValueType);
                    }
                    break;

                case IRAwait await:
                    EmitCall([await.Handle], false, null, () => this.Assembler.Call(this.Context.FunctionSymbol(this.Context.Await)), await.ValueType);
                    break;

                default:
                    throw ErrorHandler.Create($"'{invocation.GetType().Name}' is not an invocation.");
            }
        }

        readonly record struct ArgumentSource(IRValueInstruction Value, Temporary? Temporary, bool High, X8664ValueClass Class);

        bool IsDeferredArgument(IRValueInstruction argument)
            => !this.ResultSlots.ContainsKey(argument) && (argument is IRLocal || argument is IRConstantInteger constant && X8664Layout.Classify(constant.ValueType) != X8664ValueClass.Fat);

        void LoadArgument(ArgumentSource source, X8664Register destination)
        {
            if (source.Temporary is Temporary temporary)
            {
                if (temporary.InRegister)
                {
                    this.Assembler.Mov(destination, source.High ? temporary.High : temporary.Low);
                }
                else
                {
                    this.Assembler.Load(destination, Slot(temporary.Slot).Offset(source.High ? 8 : 0));
                }

                return;
            }

            switch (source.Value)
            {
                case IRLocal local when this.Homes.TryGetValue(local, out Home? home):
                    this.Assembler.Mov(destination, source.High ? home.High : home.Low);
                    break;

                case IRLocal local:
                    this.Assembler.Load(destination, SlotOf(local).Offset(source.High ? 8 : 0));
                    break;

                case IRConstantInteger constant:
                    this.Assembler.MovImmediate(destination, X8664Layout.NormalizeConstant(constant.IntegerType, constant.Value));
                    break;
            }
        }

        void LoadArgument(ArgumentSource source, X8664XmmRegister destination)
        {
            bool isDouble = source.Class == X8664ValueClass.Float64;

            if (source.Temporary is Temporary temporary)
            {
                if (temporary.InRegister)
                {
                    this.Assembler.MovXmm(destination, temporary.Xmm);
                }
                else
                {
                    this.Assembler.LoadScalar(isDouble, destination, Slot(temporary.Slot));
                }

                return;
            }

            switch (source.Value)
            {
                case IRLocal local when this.Homes.TryGetValue(local, out Home? home):
                    this.Assembler.MovXmm(destination, home.Xmm);
                    break;

                case IRLocal local:
                    this.Assembler.LoadScalar(isDouble, destination, SlotOf(local));
                    break;

                case IRConstantInteger constant:
                    long bits = isDouble ? BitConverter.DoubleToInt64Bits(constant.Value) : (uint)BitConverter.SingleToInt32Bits((float)constant.Value);
                    this.Assembler.LoadScalar(isDouble, destination, X8664Memory.Rip(this.Context.FloatConstant(bits)));
                    break;
            }
        }

        void EmitCall(List<IRValueInstruction> arguments, bool nativeAbi, IRValueInstruction? functionPointer, Action? emitCall, IRType returnType)
        {
            List<Temporary> temporaries = new List<Temporary>();
            List<ArgumentSource> positions = new List<ArgumentSource>();

            Temporary? pointerTemporary = null;

            if (functionPointer != null)
            {
                EmitValue(functionPointer);
                pointerTemporary = SaveTemporary(X8664ValueClass.Integer);
            }

            int registerCount = arguments.Sum(a => X8664Layout.Classify(a.ValueType) == X8664ValueClass.Fat && !nativeAbi ? 2 : 1);
            int lastComputed = arguments.FindLastIndex(a => !IsDeferredArgument(a));
            int directArgument = -1;
            int direct = -1;

            if (registerCount <= 4 && lastComputed >= 0 && X8664Layout.Classify(arguments[lastComputed].ValueType) is X8664ValueClass.Integer or X8664ValueClass.Float32 or X8664ValueClass.Float64)
            {
                directArgument = lastComputed;
            }

            for (int index = 0; index < arguments.Count; index++)
            {
                IRValueInstruction argument = arguments[index];
                X8664ValueClass valueClass = X8664Layout.Classify(argument.ValueType);
                Temporary? temporary = null;

                if (index == directArgument)
                {
                    EmitValue(argument);
                    direct = positions.Count;
                }
                else if (!IsDeferredArgument(argument))
                {
                    EmitValue(argument);
                    temporary = SaveTemporary(valueClass);
                    temporaries.Add(temporary);
                }

                if (valueClass == X8664ValueClass.Fat)
                {
                    positions.Add(new ArgumentSource(argument, temporary, false, X8664ValueClass.Integer));

                    if (!nativeAbi)
                    {
                        positions.Add(new ArgumentSource(argument, temporary, true, X8664ValueClass.Integer));
                    }
                }
                else
                {
                    positions.Add(new ArgumentSource(argument, temporary, false, valueClass));
                }
            }

            for (int i = 4; i < positions.Count; i++)
            {
                if (positions[i].Class is X8664ValueClass.Float32 or X8664ValueClass.Float64)
                {
                    LoadArgument(positions[i], X8664XmmRegister.XMM0);
                    this.Assembler.StoreScalar(positions[i].Class == X8664ValueClass.Float64, X8664Memory.At(X8664Register.RSP, 8 * i), X8664XmmRegister.XMM0);
                }
                else
                {
                    LoadArgument(positions[i], X8664Register.RAX);
                    this.Assembler.Store(X8664Memory.At(X8664Register.RSP, 8 * i), X8664Register.RAX);
                }
            }

            if (direct >= 0)
            {
                if (positions[direct].Class == X8664ValueClass.Integer)
                {
                    this.Assembler.Mov(IntegerArgumentRegisters[direct], X8664Register.RAX);
                }
                else
                {
                    this.Assembler.MovXmm(FloatArgumentRegisters[direct], X8664XmmRegister.XMM0);
                }
            }

            for (int i = 0; i < Math.Min(4, positions.Count); i++)
            {
                if (i == direct)
                {
                    continue;
                }

                switch (positions[i].Class)
                {
                    case X8664ValueClass.Integer:
                        LoadArgument(positions[i], IntegerArgumentRegisters[i]);
                        break;

                    case X8664ValueClass.Float32:
                    case X8664ValueClass.Float64:
                        LoadArgument(positions[i], FloatArgumentRegisters[i]);
                        break;
                }
            }

            if (pointerTemporary != null)
            {
                this.Assembler.Call(TemporaryRegister(pointerTemporary, X8664Register.R11));
            }
            else
            {
                emitCall!();
            }

            this.MaxOutgoing = Math.Max(this.MaxOutgoing, 32 + 8 * Math.Max(0, positions.Count - 4));

            for (int i = temporaries.Count - 1; i >= 0; i--)
            {
                Release(temporaries[i]);
            }

            Release(pointerTemporary);

            if (nativeAbi && X8664Layout.Classify(returnType) == X8664ValueClass.Integer)
            {
                Normalize(returnType);
            }
        }

        #endregion
    }
}
