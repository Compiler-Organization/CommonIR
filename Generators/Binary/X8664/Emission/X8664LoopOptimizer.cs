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

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal partial class X8664FunctionEmitter
    {
        sealed class ArrayRange
        {
            public IRLocal Array { get; init; } = null!;

            public IRValueInstruction? Offset { get; init; }
        }

        sealed class CountedLoop
        {
            public IRLocal Counter { get; init; } = null!;

            public IRValueInstruction Step { get; init; } = null!;

            public IRValueInstruction End { get; init; } = null!;

            public IRComparisonOperator Operator { get; init; }

            public IRStore Increment { get; init; } = null!;

            public HashSet<IRInstruction> Unchecked { get; } = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);

            public Dictionary<IRInstruction, ArrayRange> Accesses { get; } = new Dictionary<IRInstruction, ArrayRange>(ReferenceEqualityComparer.Instance);

            public List<ArrayRange> Ranges { get; } = new List<ArrayRange>();

            public IRStoreElement? Vector { get; set; }

            public HashSet<IRLocal> Assigned { get; init; } = null!;
        }

        HashSet<IRInstruction> Unchecked { get; set; } = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);

        static bool ContainsLoop(IRBlock block) => block.Instructions.Any(i => i is IRLoop || IRTree.NestedBlocks(i).Any(ContainsLoop));

        static void CountStores(IRBlock block, Dictionary<IRLocal, int> stores)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                if (instruction is IRStore { Offset: null, Target: IRLocal local })
                {
                    stores[local] = stores.GetValueOrDefault(local) + 1;
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    CountStores(nested, stores);
                }
            }
        }

        static bool SameValue(IRValueInstruction? a, IRValueInstruction? b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            return a == b || a is IRConstantInteger left && b is IRConstantInteger right && left.IntegerType == right.IntegerType && left.Value == right.Value;
        }

        CountedLoop? AnalyzeCountedLoop(IRLoop loop)
        {
            if (ContainsLoop(loop.Block) || loop.Increment != null && ContainsLoop(loop.Increment))
            {
                return null;
            }

            Dictionary<IRLocal, int> stores = new Dictionary<IRLocal, int>(ReferenceEqualityComparer.Instance);
            CountStores(loop.Block, stores);

            if (loop.Increment != null)
            {
                CountStores(loop.Increment, stores);
            }

            IRStore? increment = loop.Increment != null
                ? loop.Increment.Instructions.Count == 1 ? loop.Increment.Instructions[0] as IRStore : null
                : loop.Block.Instructions.Count > 0 ? loop.Block.Instructions[^1] as IRStore : null;

            if (increment is not { Offset: null, Target: IRLocal counter, Value: IRAdd add } || this.ResultSlots.ContainsKey(add))
            {
                return null;
            }

            IRDataTypes type = counter.ValueType.DataType;

            if (X8664Layout.Classify(counter.ValueType) != X8664ValueClass.Integer || X8664Layout.SizeOf(counter.ValueType) > 4 || type == IRDataTypes.Bool || stores.GetValueOrDefault(counter) != 1)
            {
                return null;
            }

            IRValueInstruction? step = add.Left == counter ? add.Right : add.Right == counter ? add.Left : null;

            bool IsInvariant(IRValueInstruction? value) => value is IRLocal local && local.ValueType.DataType == type && !stores.ContainsKey(local) && local != counter
                || value is IRConstantInteger constant && constant.IntegerType == type;

            if (add.ValueType.DataType != type || !IsInvariant(step) || step is IRConstantInteger { Value: <= 0 })
            {
                return null;
            }

            if (loop.Condition is not IRCompare { Operator: IRComparisonOperator.LessThan or IRComparisonOperator.LessThanOrEqual or IRComparisonOperator.NotEqualTo } condition
                || condition.Left != counter || !IsInvariant(condition.Right) || condition.OperandType.DataType != type
                || condition.Operator == IRComparisonOperator.NotEqualTo && step is IRConstantInteger { Value: not 1 })
            {
                return null;
            }

            CountedLoop counted = new CountedLoop { Counter = counter, Step = step!, End = condition.Right, Operator = condition.Operator, Increment = increment, Assigned = new HashSet<IRLocal>(stores.Keys, ReferenceEqualityComparer.Instance) };
            Dictionary<IRLocal, IRValueInstruction?> aliases = new Dictionary<IRLocal, IRValueInstruction?>(ReferenceEqualityComparer.Instance);

            bool TryOffset(IRValueInstruction index, out IRValueInstruction? offset)
            {
                offset = null;

                if (index.ValueType.DataType != type || this.ResultSlots.ContainsKey(index))
                {
                    return false;
                }

                if (index == counter)
                {
                    return true;
                }

                if (index is IRLocal alias && aliases.TryGetValue(alias, out offset))
                {
                    return true;
                }

                if (index is IRAdd { Left: IRValueInstruction left, Right: IRValueInstruction right } sum && sum.ValueType.DataType == type)
                {
                    if (left == counter && IsInvariant(right))
                    {
                        offset = right;
                        return true;
                    }

                    if (right == counter && IsInvariant(left))
                    {
                        offset = left;
                        return true;
                    }
                }

                return false;
            }

            void AddAccess(IRInstruction access, IRValueInstruction target, IRValueInstruction index)
            {
                if (target is IRLocal array && !stores.ContainsKey(array) && TryOffset(index, out IRValueInstruction? offset))
                {
                    ArrayRange? range = counted.Ranges.FirstOrDefault(r => r.Array == array && SameValue(r.Offset, offset));

                    if (range == null)
                    {
                        range = new ArrayRange { Array = array, Offset = offset };
                        counted.Ranges.Add(range);
                    }

                    counted.Unchecked.Add(access);
                    counted.Accesses[access] = range;
                }
            }

            void Visit(IRInstruction instruction)
            {
                switch (instruction)
                {
                    case IRLoadElement load:
                        AddAccess(load, load.Target, load.Index);
                        break;

                    case IRStoreElement store:
                        AddAccess(store, store.Target, store.Index);
                        break;
                }

                foreach (IRValueInstruction child in IRTree.Children(instruction))
                {
                    if (!this.ResultSlots.ContainsKey(child))
                    {
                        Visit(child);
                    }
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    foreach (IRInstruction statement in nested.Instructions)
                    {
                        Visit(statement);
                    }
                }
            }

            foreach (IRInstruction statement in loop.Block.Instructions)
            {
                if (statement == increment)
                {
                    continue;
                }

                Visit(statement);

                if (statement is IRStore { Offset: null, Target: IRLocal alias, Value: IRValueInstruction value } && alias != counter && stores.GetValueOrDefault(alias) == 1 && TryOffset(value, out IRValueInstruction? aliasOffset))
                {
                    aliases[alias] = aliasOffset;
                }
            }

            Visit(loop.Condition);

            if (counted.Unchecked.Count == 0)
            {
                return null;
            }

            counted.Vector = FindVectorStore(loop, counted, aliases);
            return counted;
        }

        IRStoreElement? FindVectorStore(IRLoop loop, CountedLoop counted, Dictionary<IRLocal, IRValueInstruction?> aliases)
        {
            if (loop.Increment == null)
            {
                return null;
            }

            IRStoreElement? vector = null;

            foreach (IRInstruction statement in loop.Block.Instructions)
            {
                switch (statement)
                {
                    case IRStore { Offset: null, Target: IRLocal alias } when aliases.ContainsKey(alias):
                        break;

                    case IRStoreElement store when vector == null && counted.Unchecked.Contains(store) && X8664Layout.Classify(store.ElementType) is X8664ValueClass.Float32 or X8664ValueClass.Float64:
                        vector = store;
                        break;

                    default:
                        return null;
                }
            }

            if (vector == null || PackedDepth(vector.Value, vector.ElementType, counted) is not int depth || depth > 6)
            {
                return null;
            }

            return vector;
        }

        int? PackedDepth(IRValueInstruction value, IRType type, CountedLoop counted)
        {
            if (value.ValueType.DataType != type.DataType || this.ResultSlots.ContainsKey(value))
            {
                return null;
            }

            switch (value)
            {
                case IRLoadElement load when counted.Unchecked.Contains(load) && load.ElementType.DataType == type.DataType:
                case IRConstantInteger:
                    return 1;

                case IRLocal local when !counted.Assigned.Contains(local):
                    return 1;

                case IRAdd or IRSubtract or IRMultiply or IRDivide:
                    {
                        IRValueInstruction[] children = IRTree.Children(value).ToArray();

                        if (PackedDepth(children[0], type, counted) is not int left || PackedDepth(children[1], type, counted) is not int right)
                        {
                            return null;
                        }

                        return Math.Max(left, right + 1);
                    }

                default:
                    return null;
            }
        }

        void EmitCountedLoop(IRLoop loop, CountedLoop counted)
        {
            X8664Symbol checkedLoop = NewLabel("checked");
            X8664Symbol done = NewLabel("versioned");
            bool signed = X8664Layout.IsSigned(counted.Counter.ValueType);

            if (counted.Step is IRLocal)
            {
                LoadInteger(counted.Step, X8664Register.RAX);
                this.Assembler.Test(X8664Register.RAX, X8664Register.RAX);
                this.Assembler.JumpIf(signed ? X8664Condition.LessOrEqual : X8664Condition.Equal, checkedLoop);

                if (counted.Operator == IRComparisonOperator.NotEqualTo)
                {
                    this.Assembler.AluImmediate(X8664AluOperation.Compare, X8664Register.RAX, 1);
                    this.Assembler.JumpIf(X8664Condition.NotEqual, checkedLoop);
                }
            }

            LoadInteger(counted.Counter, X8664Register.R8);
            LoadInteger(counted.End, X8664Register.R9);

            if (counted.Operator == IRComparisonOperator.NotEqualTo)
            {
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R8, X8664Register.R9);
                this.Assembler.JumpIf(signed ? X8664Condition.GreaterOrEqual : X8664Condition.AboveOrEqual, checkedLoop);
            }

            long maximum = counted.Counter.ValueType.DataType switch
            {
                IRDataTypes.Int8 => sbyte.MaxValue,
                IRDataTypes.UInt8 => byte.MaxValue,
                IRDataTypes.Int16 => short.MaxValue,
                IRDataTypes.UInt16 => ushort.MaxValue,
                IRDataTypes.Int32 => int.MaxValue,
                _ => uint.MaxValue,
            };

            LoadInteger(counted.Step, X8664Register.RAX);
            this.Assembler.Alu(X8664AluOperation.Add, X8664Register.RAX, X8664Register.R9);
            this.Assembler.MovImmediate(X8664Register.RCX, maximum);
            this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, X8664Register.RCX);
            this.Assembler.JumpIf(X8664Condition.Greater, checkedLoop);

            if (counted.Operator != IRComparisonOperator.LessThanOrEqual)
            {
                this.Assembler.AluImmediate(X8664AluOperation.Subtract, X8664Register.R9, 1);
            }

            foreach (ArrayRange range in counted.Ranges)
            {
                FatSource array = ArraySource(range.Array);

                this.Assembler.Mov(X8664Register.R10, X8664Register.R8);
                this.Assembler.Mov(X8664Register.R11, X8664Register.R9);

                if (range.Offset != null)
                {
                    LoadInteger(range.Offset, X8664Register.RAX);
                    this.Assembler.Alu(X8664AluOperation.Add, X8664Register.R10, X8664Register.RAX);
                    this.Assembler.Alu(X8664AluOperation.Add, X8664Register.R11, X8664Register.RAX);
                }

                this.Assembler.Test(X8664Register.R10, X8664Register.R10);
                this.Assembler.JumpIf(X8664Condition.Sign, checkedLoop);

                if (array.InRegisters)
                {
                    this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R11, array.Length);
                }
                else
                {
                    this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R11, array.Memory.Offset(8));
                }

                this.Assembler.JumpIf(X8664Condition.AboveOrEqual, checkedLoop);
            }

            HashSet<IRInstruction> outer = this.Unchecked;
            this.Unchecked = new HashSet<IRInstruction>(outer.Concat(counted.Unchecked), ReferenceEqualityComparer.Instance);

            if (counted.Vector != null)
            {
                EmitVectorLoop(counted, counted.Vector);
            }

            EmitLoopCore(loop);
            this.Unchecked = outer;
            this.Assembler.Jump(done);

            this.Assembler.Bind(checkedLoop);
            EmitLoopCore(loop);
            this.Assembler.Bind(done);
        }

        void EmitVectorLoop(CountedLoop counted, IRStoreElement store)
        {
            bool isDouble = X8664Layout.Classify(store.ElementType) == X8664ValueClass.Float64;
            int lanes = isDouble ? 2 : 4;
            int width = X8664Layout.SizeOf(store.ElementType);
            ArrayRange target = counted.Accesses[store];
            X8664Symbol skip = NewLabel("vector.skip");
            X8664Symbol top = NewLabel("vector");
            X8664Symbol check = NewLabel("vector.check");
            bool signed = X8664Layout.IsSigned(counted.Counter.ValueType);

            List<IRLoadElement> loads = new List<IRLoadElement>();
            CollectLoads(store.Value, loads);

            List<ArrayRange> ranges = loads.Select(l => counted.Accesses[l]).Append(target).Distinct().ToList();
            List<IRValueInstruction> invariants = new List<IRValueInstruction>();
            CollectInvariants(store.Value, invariants);

            if (ranges.Count > BaseRegisters.Length || invariants.Count + (PackedDepth(store.Value, store.ElementType, counted) ?? 99) > PackedRegisters.Length
                || counted.End is not (IRLocal or IRConstantInteger))
            {
                return;
            }

            if (counted.Step is IRLocal)
            {
                LoadInteger(counted.Step, X8664Register.RAX);
                this.Assembler.AluImmediate(X8664AluOperation.Compare, X8664Register.RAX, 1);
                this.Assembler.JumpIf(X8664Condition.NotEqual, skip);
            }
            else if (counted.Step is not IRConstantInteger { Value: 1 })
            {
                return;
            }

            foreach (IRLoadElement load in loads)
            {
                ArrayRange source = counted.Accesses[load];

                if (source.Array == target.Array && SameValue(source.Offset, target.Offset))
                {
                    continue;
                }

                X8664Symbol distinct = NewLabel("vector.distinct");
                LoadArrayPointer(source.Array, X8664Register.R10);
                LoadArrayPointer(target.Array, X8664Register.R11);
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R10, X8664Register.R11);
                this.Assembler.JumpIf(X8664Condition.NotEqual, distinct);
                LoadOffset(source.Offset, X8664Register.R10);
                LoadOffset(target.Offset, X8664Register.R11);
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.R10, X8664Register.R11);
                this.Assembler.JumpIf(X8664Condition.NotEqual, skip);
                this.Assembler.Bind(distinct);
            }

            Dictionary<ArrayRange, X8664Register> bases = new Dictionary<ArrayRange, X8664Register>();

            for (int i = 0; i < ranges.Count; i++)
            {
                X8664Register register = BaseRegisters[i];
                LoadOffset(ranges[i].Offset, X8664Register.RAX);
                this.Assembler.ImulImmediate(X8664Register.RAX, X8664Register.RAX, width);
                LoadArrayPointer(ranges[i].Array, register);
                this.Assembler.Alu(X8664AluOperation.Add, register, X8664Register.RAX);
                bases[ranges[i]] = register;
            }

            List<(IRValueInstruction Value, X8664XmmRegister Register)> broadcasts = new List<(IRValueInstruction Value, X8664XmmRegister Register)>();

            for (int i = 0; i < invariants.Count; i++)
            {
                X8664XmmRegister register = PackedRegisters[PackedRegisters.Length - 1 - i];
                LoadBroadcast(invariants[i], register, isDouble);
                broadcasts.Add((invariants[i], register));
            }

            X8664Register index = RegisterHome(counted.Counter) is Home home ? home.Low : X8664Register.RAX;

            this.Assembler.Jump(check);
            this.Assembler.Align(16);
            this.Assembler.Bind(top);

            if (index == X8664Register.RAX)
            {
                LoadInteger(counted.Counter, X8664Register.RAX);
            }

            for (int unroll = 0; unroll < 2; unroll++)
            {
                int displacement = unroll * lanes * width;
                EmitPacked(store.Value, 0, isDouble, counted, bases, broadcasts, index, displacement);
                this.Assembler.StorePacked(X8664Memory.Indexed(bases[target], index, width, displacement), X8664XmmRegister.XMM0);
            }

            if (index == X8664Register.RAX)
            {
                this.Assembler.AluImmediate(X8664AluOperation.Add, X8664Register.RAX, 2 * lanes);
                this.Assembler.Store(SlotOf(counted.Counter), X8664Register.RAX);
            }
            else
            {
                this.Assembler.AluImmediate(X8664AluOperation.Add, index, 2 * lanes);
            }

            this.Assembler.Bind(check);

            if (index == X8664Register.RAX)
            {
                LoadInteger(counted.Counter, X8664Register.RAX);
            }

            this.Assembler.Lea(X8664Register.RAX, X8664Memory.At(index, 2 * lanes));

            if (counted.End is IRConstantInteger end && FitsImmediate(end, out int immediate))
            {
                this.Assembler.AluImmediate(X8664AluOperation.Compare, X8664Register.RAX, immediate);
            }
            else if (RegisterHome(counted.End) is Home endHome)
            {
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, endHome.Low);
            }
            else if (counted.End is IRLocal endLocal)
            {
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, SlotOf(endLocal));
            }
            else
            {
                this.Assembler.MovImmediate(X8664Register.R11, X8664Layout.NormalizeConstant(((IRConstantInteger)counted.End).IntegerType, ((IRConstantInteger)counted.End).Value));
                this.Assembler.Alu(X8664AluOperation.Compare, X8664Register.RAX, X8664Register.R11);
            }

            X8664Condition again = counted.Operator == IRComparisonOperator.LessThanOrEqual
                ? signed ? X8664Condition.LessOrEqual : X8664Condition.BelowOrEqual
                : signed ? X8664Condition.Less : X8664Condition.Below;

            this.Assembler.JumpIf(again, top);
            this.Assembler.Bind(skip);
        }

        static readonly X8664Register[] BaseRegisters = [X8664Register.R8, X8664Register.R9, X8664Register.R10, X8664Register.R11, X8664Register.RDX, X8664Register.RCX];

        static void CollectInvariants(IRValueInstruction value, List<IRValueInstruction> invariants)
        {
            switch (value)
            {
                case IRLoadElement:
                    return;

                case IRLocal or IRConstantInteger:
                    if (!invariants.Any(i => SameValue(i, value)))
                    {
                        invariants.Add(value);
                    }
                    return;
            }

            foreach (IRValueInstruction child in IRTree.Children(value))
            {
                CollectInvariants(child, invariants);
            }
        }

        void LoadBroadcast(IRValueInstruction value, X8664XmmRegister register, bool isDouble)
        {
            switch (value)
            {
                case IRConstantInteger constant:
                    {
                        long bits = isDouble ? BitConverter.DoubleToInt64Bits(constant.Value) : (uint)BitConverter.SingleToInt32Bits((float)constant.Value);
                        this.Assembler.LoadScalar(isDouble, register, X8664Memory.Rip(this.Context.FloatConstant(bits)));
                    }
                    break;

                case IRLocal local when RegisterHome(local) is Home home:
                    this.Assembler.MovXmm(register, home.Xmm);
                    break;

                case IRLocal local:
                    this.Assembler.LoadScalar(isDouble, register, SlotOf(local));
                    break;
            }

            this.Assembler.Broadcast(isDouble, register);
        }

        static void CollectLoads(IRValueInstruction value, List<IRLoadElement> loads)
        {
            if (value is IRLoadElement load)
            {
                loads.Add(load);
                return;
            }

            foreach (IRValueInstruction child in IRTree.Children(value))
            {
                CollectLoads(child, loads);
            }
        }

        void LoadArrayPointer(IRLocal array, X8664Register destination)
        {
            if (RegisterHome(array) is Home home)
            {
                this.Assembler.Mov(destination, home.Low);
            }
            else
            {
                this.Assembler.Load(destination, SlotOf(array));
            }
        }

        void LoadOffset(IRValueInstruction? offset, X8664Register destination)
        {
            if (offset == null)
            {
                this.Assembler.MovImmediate(destination, 0);
            }
            else
            {
                LoadInteger(offset, destination);
            }
        }

        static readonly X8664XmmRegister[] PackedRegisters = [X8664XmmRegister.XMM0, X8664XmmRegister.XMM1, X8664XmmRegister.XMM2, X8664XmmRegister.XMM3, X8664XmmRegister.XMM4, X8664XmmRegister.XMM5];

        void EmitPacked(IRValueInstruction value, int depth, bool isDouble, CountedLoop counted, Dictionary<ArrayRange, X8664Register> bases, List<(IRValueInstruction Value, X8664XmmRegister Register)> broadcasts, X8664Register index, int displacement)
        {
            X8664XmmRegister register = PackedRegisters[depth];

            X8664XmmRegister? Invariant(IRValueInstruction candidate)
            {
                foreach ((IRValueInstruction invariant, X8664XmmRegister held) in broadcasts)
                {
                    if (SameValue(invariant, candidate))
                    {
                        return held;
                    }
                }

                return null;
            }

            if (Invariant(value) is X8664XmmRegister operand)
            {
                this.Assembler.MovXmm(register, operand);
                return;
            }

            if (value is IRLoadElement load)
            {
                this.Assembler.LoadPacked(register, X8664Memory.Indexed(bases[counted.Accesses[load]], index, X8664Layout.SizeOf(load.ElementType), displacement));
                return;
            }

            IRValueInstruction[] children = IRTree.Children(value).ToArray();
            X8664SseOperation operation = value switch
            {
                IRAdd => X8664SseOperation.Add,
                IRSubtract => X8664SseOperation.Subtract,
                IRMultiply => X8664SseOperation.Multiply,
                IRDivide => X8664SseOperation.Divide,
                _ => throw ErrorHandler.Create($"'{value.GetType().Name}' cannot be vectorized.")
            };

            EmitPacked(children[0], depth, isDouble, counted, bases, broadcasts, index, displacement);

            if (Invariant(children[1]) is X8664XmmRegister right)
            {
                this.Assembler.PackedArithmetic(operation, isDouble, register, right);
                return;
            }

            EmitPacked(children[1], depth + 1, isDouble, counted, bases, broadcasts, index, displacement);
            this.Assembler.PackedArithmetic(operation, isDouble, register, PackedRegisters[depth + 1]);
        }
    }
}
