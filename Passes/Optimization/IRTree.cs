using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    internal enum IRSlotKind
    {
        Value,
        Address,
        StoreTarget,
        ArraySize,
    }

    internal sealed class IRSlot
    {
        readonly IRInstruction Parent;
        readonly Func<IRValueInstruction?> Getter;
        readonly Action<IRValueInstruction> Setter;

        public IRSlotKind Kind { get; }

        public IRSlot(IRInstruction parent, IRSlotKind kind, Func<IRValueInstruction?> getter, Action<IRValueInstruction> setter)
        {
            this.Parent = parent;
            this.Kind = kind;
            this.Getter = getter;
            this.Setter = setter;
        }

        public IRValueInstruction? Value => this.Getter();

        public void Replace(IRValueInstruction replacement)
        {
            IRValueInstruction? old = this.Getter();

            if (old == null || ReferenceEquals(old, replacement))
            {
                return;
            }

            this.Setter(replacement);

            int index = this.Parent.Operands.FindIndex(o => ReferenceEquals(o, old));

            if (index >= 0)
            {
                this.Parent.Operands[index] = replacement;
            }

            if (old.References.Remove(this.Parent))
            {
                replacement.References.Add(this.Parent);
            }
        }
    }

    internal static class IRTree
    {
        public static bool IsInvocation(IRInstruction instruction)
            => instruction is IRCall or IRCallIndirect or IRDispatch or IRAwait or IRBytes;

        public static bool IsLeaf(IRInstruction instruction)
            => instruction is IRLocal or IRGlobal or IRConstantInteger or IRConstantString or IRConstantBytes
                or IRStructProperty or IRFunctionReference or IRBreak or IRContinue or IRSizeOf;

        public static bool IsTerminator(IRInstruction instruction)
            => instruction is IRReturn or IRBreak or IRContinue or IRPanic;

        public static bool IsKnown(IRInstruction instruction)
            => IsLeaf(instruction) || IsInvocation(instruction) || instruction is IRAdd or IRSubtract or IRMultiply or IRDivide
                or IRBitwise or IRCompare or IRConvert or IRMath or IRLoad or IRStore or IRLoadElement or IRStoreElement or IRLength
                or IRFatPointer or IRReturn or IRConditionalBranch or IRLoop or IRFree or IRPanic or IRMalloc
                or IRInstantiateArray or IRInstantiateStruct;

        public static List<IRSlot> Slots(IRInstruction instruction)
        {
            List<IRSlot> slots = new List<IRSlot>();

            void Add(IRSlotKind kind, Func<IRValueInstruction?> getter, Action<IRValueInstruction> setter)
                => slots.Add(new IRSlot(instruction, kind, getter, setter));

            void AddList(IRSlotKind kind, List<IRValueInstruction>? list)
            {
                if (list == null)
                {
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    int index = i;
                    Add(kind, () => list[index], v => list[index] = v);
                }
            }

            switch (instruction)
            {
                case IRAdd add:
                    Add(IRSlotKind.Value, () => add.Left, v => add.Left = v);
                    Add(IRSlotKind.Value, () => add.Right, v => add.Right = v);
                    break;

                case IRSubtract subtract:
                    Add(IRSlotKind.Value, () => subtract.Left, v => subtract.Left = v);
                    Add(IRSlotKind.Value, () => subtract.Right, v => subtract.Right = v);
                    break;

                case IRMultiply multiply:
                    Add(IRSlotKind.Value, () => multiply.Left, v => multiply.Left = v);
                    Add(IRSlotKind.Value, () => multiply.Right, v => multiply.Right = v);
                    break;

                case IRDivide divide:
                    Add(IRSlotKind.Value, () => divide.Left, v => divide.Left = v);
                    Add(IRSlotKind.Value, () => divide.Right, v => divide.Right = v);
                    break;

                case IRBitwise bitwise:
                    Add(IRSlotKind.Value, () => bitwise.Left, v => bitwise.Left = v);
                    Add(IRSlotKind.Value, () => bitwise.Right, v => bitwise.Right = v);
                    break;

                case IRCompare compare:
                    Add(IRSlotKind.Value, () => compare.Left, v => compare.Left = v);
                    Add(IRSlotKind.Value, () => compare.Right, v => compare.Right = v);
                    break;

                case IRConvert convert:
                    Add(IRSlotKind.Value, () => convert.Value, v => convert.Value = v);
                    break;

                case IRMath math:
                    Add(IRSlotKind.Value, () => math.Value, v => math.Value = v);
                    break;

                case IRLoad load:
                    Add(IRSlotKind.Address, () => load.Target, v => load.Target = v);
                    if (load.Offset != null)
                    {
                        Add(IRSlotKind.Value, () => load.Offset, v => load.Offset = v);
                    }
                    break;

                case IRStore store:
                    Add(store.Offset == null ? IRSlotKind.StoreTarget : IRSlotKind.Address, () => store.Target, v => store.Target = v);
                    if (store.Offset != null)
                    {
                        Add(IRSlotKind.Value, () => store.Offset, v => store.Offset = v);
                    }
                    Add(IRSlotKind.Value, () => store.Value, v => store.Value = v);
                    break;

                case IRLoadElement loadElement:
                    Add(IRSlotKind.Address, () => loadElement.Target, v => loadElement.Target = v);
                    Add(IRSlotKind.Value, () => loadElement.Index, v => loadElement.Index = v);
                    break;

                case IRStoreElement storeElement:
                    Add(IRSlotKind.Address, () => storeElement.Target, v => storeElement.Target = v);
                    Add(IRSlotKind.Value, () => storeElement.Index, v => storeElement.Index = v);
                    Add(IRSlotKind.Value, () => storeElement.Value, v => storeElement.Value = v);
                    break;

                case IRLength length:
                    Add(IRSlotKind.Address, () => length.Target, v => length.Target = v);
                    break;

                case IRFatPointer fatPointer:
                    Add(IRSlotKind.Value, () => fatPointer.Pointer, v => fatPointer.Pointer = v);
                    Add(IRSlotKind.Value, () => fatPointer.Length, v => fatPointer.Length = v);
                    break;

                case IRCall call:
                    AddList(IRSlotKind.Value, call.Arguments);
                    break;

                case IRCallIndirect callIndirect:
                    Add(IRSlotKind.Address, () => callIndirect.FunctionPointer, v => callIndirect.FunctionPointer = v);
                    AddList(IRSlotKind.Value, callIndirect.Arguments);
                    break;

                case IRDispatch dispatch:
                    Add(IRSlotKind.Value, () => dispatch.Count, v => dispatch.Count = v);
                    AddList(IRSlotKind.Value, dispatch.Arguments);
                    AddList(IRSlotKind.Value, dispatch.Residents);
                    break;

                case IRAwait await:
                    Add(IRSlotKind.Address, () => await.Handle, v => await.Handle = v);
                    break;

                case IRReturn ret:
                    AddList(IRSlotKind.Value, ret.Values);
                    break;

                case IRConditionalBranch conditionalBranch:
                    Add(IRSlotKind.Value, () => conditionalBranch.Condition, v => conditionalBranch.Condition = v);
                    break;

                case IRLoop loop:
                    Add(IRSlotKind.Value, () => loop.Condition, v => loop.Condition = v);
                    break;

                case IRFree free:
                    Add(IRSlotKind.Address, () => free.Pointer, v => free.Pointer = v);
                    break;

                case IRPanic panic:
                    Add(IRSlotKind.Address, () => panic.Message, v => panic.Message = v);
                    break;

                case IRMalloc malloc:
                    Add(IRSlotKind.Value, () => malloc.Bytes, v => malloc.Bytes = v);
                    break;

                case IRInstantiateArray instantiateArray:
                    Add(IRSlotKind.ArraySize, () => instantiateArray.Size, v => instantiateArray.Size = v);
                    AddList(IRSlotKind.Value, instantiateArray.ElementValues);
                    break;

                case IRInstantiateStruct instantiateStruct:
                    AddList(IRSlotKind.Value, instantiateStruct.PropertyValues);
                    break;
            }

            return slots;
        }

        public static IEnumerable<IRValueInstruction> Children(IRInstruction instruction)
            => Slots(instruction).Select(s => s.Value).OfType<IRValueInstruction>();

        public static IEnumerable<IRBlock> NestedBlocks(IRInstruction instruction)
        {
            switch (instruction)
            {
                case IRConditionalBranch conditionalBranch:
                    yield return conditionalBranch.ThenBlock;
                    if (conditionalBranch.HasElseBlock && conditionalBranch.ElseBlock != null)
                    {
                        yield return conditionalBranch.ElseBlock;
                    }
                    break;

                case IRLoop loop:
                    yield return loop.Block;
                    if (loop.Increment != null)
                    {
                        yield return loop.Increment;
                    }
                    break;
            }
        }

        public static bool AlwaysExits(IRBlock block)
            => block.Instructions.Any(i => IsTerminator(i)
                || (i is IRConditionalBranch branch && branch.HasElseBlock && branch.ElseBlock != null && AlwaysExits(branch.ThenBlock) && AlwaysExits(branch.ElseBlock)));

        public static bool IsPure(IRValueInstruction value, IRFunctionAnalysis analysis)
        {
            switch (value)
            {
                case IRConstantInteger or IRConstantString or IRConstantBytes or IRLocal or IRGlobal or IRStructProperty or IRFunctionReference:
                    return true;

                case IRCall or IRCallIndirect or IRDispatch or IRAwait or IRBytes:
                    return analysis.Placed.Contains(value);

                case IRAdd or IRSubtract or IRMultiply or IRBitwise or IRCompare or IRConvert or IRMath or IRFatPointer or IRLength:
                    return Children(value).All(c => IsPure(c, analysis));

                case IRDivide divide:
                    return IsSafeDivisor(divide) && IsPure(divide.Left, analysis) && IsPure(divide.Right, analysis);

                case IRLoad load:
                    return load.Offset == null && load.Target is IRLocal or IRGlobal;

                default:
                    return false;
            }
        }

        static bool IsSafeDivisor(IRDivide divide)
        {
            IRDataTypes type = divide.ValueType.DataType;

            if (IRNumeric.IsFloat(type))
            {
                return true;
            }

            if (!IRNumeric.IsInteger(type) || !IRNumeric.TryGetInteger(divide.Right, type, out long divisor))
            {
                return false;
            }

            return divisor != 0 && !(divisor == -1 && IRNumeric.IsSigned(type));
        }

        public static bool AreEquivalent(IRValueInstruction a, IRValueInstruction b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a.GetType() != b.GetType() || a.ValueType.DataType != b.ValueType.DataType)
            {
                return false;
            }

            switch (a, b)
            {
                case (IRConstantInteger x, IRConstantInteger y):
                    return x.IntegerType == y.IntegerType && BitConverter.DoubleToInt64Bits(x.Value) == BitConverter.DoubleToInt64Bits(y.Value);

                case (IRBitwise x, IRBitwise y):
                    return x.Operator == y.Operator && AreEquivalent(x.Left, y.Left) && AreEquivalent(x.Right, y.Right);

                case (IRCompare x, IRCompare y):
                    return x.Operator == y.Operator && x.OperandType.DataType == y.OperandType.DataType && AreEquivalent(x.Left, y.Left) && AreEquivalent(x.Right, y.Right);

                case (IRAdd or IRSubtract or IRMultiply or IRDivide, _):
                    {
                        List<IRValueInstruction> left = Children(a).ToList();
                        List<IRValueInstruction> right = Children(b).ToList();
                        return AreEquivalent(left[0], right[0]) && AreEquivalent(left[1], right[1]);
                    }

                case (IRConvert x, IRConvert y):
                    return x.Value.ValueType.DataType == y.Value.ValueType.DataType && AreEquivalent(x.Value, y.Value);

                case (IRLoad x, IRLoad y):
                    return x.Offset == null && y.Offset == null && ReferenceEquals(x.Target, y.Target) && x.TargetType.DataType == y.TargetType.DataType;

                default:
                    return false;
            }
        }

        public static void Release(IRValueInstruction value, IRInstruction user, IRFunctionAnalysis? analysis, HashSet<IRInstruction>? kept = null)
        {
            value.References.Remove(user);

            if (kept != null && kept.Contains(value))
            {
                return;
            }

            if (analysis != null ? analysis.ReleaseUse(value) < analysis.UsesOf(value) : value.References.Count > 0)
            {
                return;
            }

            if (value is IRFunctionReference functionReference)
            {
                functionReference.Function.References.Remove(functionReference);
                return;
            }

            if (IsLeaf(value) || (IsInvocation(value) && analysis != null && analysis.Placed.Contains(value)))
            {
                return;
            }

            ReleaseOperands(value, analysis, kept);
        }

        public static HashSet<IRInstruction> Reachable(IRInstruction root)
        {
            HashSet<IRInstruction> reached = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);
            Stack<IRInstruction> pending = new Stack<IRInstruction>([root]);

            while (pending.Count > 0)
            {
                IRInstruction current = pending.Pop();

                if (reached.Add(current))
                {
                    foreach (IRValueInstruction child in Children(current))
                    {
                        pending.Push(child);
                    }
                }
            }

            return reached;
        }

        public static void ReleaseOperands(IRInstruction instruction, IRFunctionAnalysis? analysis, HashSet<IRInstruction>? kept = null)
        {
            switch (instruction)
            {
                case IRCall call:
                    call.Function.References.Remove(call);
                    break;

                case IRDispatch dispatch:
                    dispatch.Kernel.References.Remove(dispatch);
                    break;
            }

            foreach (IRValueInstruction child in Children(instruction))
            {
                Release(child, instruction, analysis, kept);
            }
        }

        public static void ReleaseStatement(IRInstruction statement, IRFunctionAnalysis analysis)
        {
            ReleaseOperands(statement, analysis);

            foreach (IRBlock block in NestedBlocks(statement))
            {
                foreach (IRInstruction inner in block.Instructions)
                {
                    ReleaseStatement(inner, analysis);
                }
            }
        }
    }

    internal sealed class IRFunctionAnalysis
    {
        public IRFunction Function { get; }

        public HashSet<IRInstruction> Placed { get; } = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);

        public Dictionary<IRValueInstruction, int> Uses { get; } = new Dictionary<IRValueInstruction, int>(ReferenceEqualityComparer.Instance);

        public bool IsSupported { get; private set; } = true;

        public IRFunctionAnalysis(IRFunction function)
        {
            this.Function = function;

            CollectPlaced(function.Entryblock);

            if (this.IsSupported)
            {
                CountBlock(function.Entryblock);
            }
        }

        public int UsesOf(IRValueInstruction value) => this.Uses.GetValueOrDefault(value);

        Dictionary<IRValueInstruction, int> Released { get; } = new Dictionary<IRValueInstruction, int>(ReferenceEqualityComparer.Instance);

        public int ReleaseUse(IRValueInstruction value)
        {
            int released = this.Released.GetValueOrDefault(value) + 1;
            this.Released[value] = released;
            return released;
        }

        public bool IsUnshared(IRValueInstruction value) => this.Uses.GetValueOrDefault(value, 1) <= 1;

        void CollectPlaced(IRBlock block)
        {
            if (block.ReturnType.DataType != IRDataTypes.Void)
            {
                this.IsSupported = false;
            }

            foreach (IRInstruction instruction in block.Instructions)
            {
                if (!IRTree.IsKnown(instruction))
                {
                    this.IsSupported = false;
                }

                this.Placed.Add(instruction);

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    CollectPlaced(nested);
                }
            }
        }

        void CountBlock(IRBlock block)
        {
            IRUseCounter counter = new IRUseCounter(this.Placed);
            counter.CountStatements(block.Instructions);

            foreach (KeyValuePair<IRValueInstruction, int> use in counter.Uses)
            {
                this.Uses[use.Key] = use.Value;
            }

            if (counter.SawUnknown)
            {
                this.IsSupported = false;
            }
        }
    }

    internal sealed class IRUseCounter
    {
        readonly HashSet<IRInstruction> Placed;

        readonly HashSet<IRInstruction> Visited = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);

        public Dictionary<IRValueInstruction, int> Uses { get; } = new Dictionary<IRValueInstruction, int>(ReferenceEqualityComparer.Instance);

        public List<IRValueInstruction> Produced { get; } = new List<IRValueInstruction>();

        public bool SawUnknown { get; private set; }

        public IRUseCounter(HashSet<IRInstruction> placed)
        {
            this.Placed = placed;
        }

        public void CountStatements(List<IRInstruction> statements)
        {
            foreach (IRInstruction statement in statements)
            {
                if (statement is IRValueInstruction value)
                {
                    this.Produced.Add(value);
                }

                if (this.Visited.Add(statement))
                {
                    CountOperands(statement);
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(statement))
                {
                    CountStatements(nested.Instructions);
                }
            }
        }

        void CountOperands(IRInstruction instruction)
        {
            if (instruction is IRSizeOf sizeOf && sizeOf.Target is IRValueInstruction sized)
            {
                this.Uses[sized] = this.Uses.GetValueOrDefault(sized) + 1;
            }

            foreach (IRValueInstruction child in IRTree.Children(instruction))
            {
                if (!IRTree.IsKnown(child))
                {
                    this.SawUnknown = true;
                    continue;
                }

                this.Uses[child] = this.Uses.GetValueOrDefault(child) + 1;

                if (!(IRTree.IsInvocation(child) && this.Placed.Contains(child)) && this.Visited.Add(child))
                {
                    CountOperands(child);
                }
            }
        }
    }
}
