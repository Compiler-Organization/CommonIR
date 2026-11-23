using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    /// <summary>
    /// Computes expressions that give the same value in every iteration of a loop once, before the loop. Only
    /// expressions that read nothing but constants and locals the loop never assigns, and that cannot trap, are moved,
    /// so running them when the loop runs zero times changes nothing.
    /// </summary>
    public class IRLoopInvariantMover : IRPass
    {
        IRModule Module { get; set; }

        IRFunctionAnalysis Analysis { get; set; } = null!;

        IRFunction Function { get; set; } = null!;

        int Count { get; set; }

        public IRLoopInvariantMover(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            bool changed = false;

            foreach (IRFunction function in this.Module.Functions)
            {
                if (function.IsKernel)
                {
                    continue;
                }

                this.Analysis = new IRFunctionAnalysis(function);

                if (!this.Analysis.IsSupported)
                {
                    continue;
                }

                this.Function = function;
                changed |= VisitBlock(function.Entryblock);
            }

            return changed;
        }

        bool VisitBlock(IRBlock block)
        {
            bool changed = false;

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                IRInstruction instruction = block.Instructions[i];

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    changed |= VisitBlock(nested);
                }

                if (instruction is IRLoop loop)
                {
                    int moved = HoistFrom(block, i, loop);

                    if (moved > 0)
                    {
                        i += moved;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        int HoistFrom(IRBlock parent, int index, IRLoop loop)
        {
            HashSet<IRLocal> assigned = new HashSet<IRLocal>(ReferenceEqualityComparer.Instance);
            CollectAssigned(loop.Block, assigned);

            if (loop.Increment != null)
            {
                CollectAssigned(loop.Increment, assigned);
            }

            List<IRStore> hoisted = new List<IRStore>();

            void VisitSlots(IRInstruction instruction)
            {
                foreach (IRSlot slot in IRTree.Slots(instruction))
                {
                    if (slot.Kind == IRSlotKind.StoreTarget || slot.Value is not IRValueInstruction value)
                    {
                        continue;
                    }

                    if (!IRTree.IsLeaf(value) && IsInvariant(value, assigned))
                    {
                        IRLocal local = this.Function.CreateLocal($"$invariant{this.Count++}", value.ValueType, true);
                        slot.Replace(local);

                        IRStore store = new IRStore(local, value) { Parent = parent };
                        hoisted.Add(store);
                    }
                    else if (!this.Analysis.Placed.Contains(value))
                    {
                        VisitSlots(value);
                    }
                }
            }

            void VisitStatements(IRBlock block)
            {
                foreach (IRInstruction instruction in block.Instructions)
                {
                    VisitSlots(instruction);

                    foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                    {
                        VisitStatements(nested);
                    }
                }
            }

            VisitSlots(loop);
            VisitStatements(loop.Block);

            if (loop.Increment != null)
            {
                VisitStatements(loop.Increment);
            }

            parent.Instructions.InsertRange(index, hoisted);
            return hoisted.Count;
        }

        static void CollectAssigned(IRBlock block, HashSet<IRLocal> assigned)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                if (instruction is IRStore { Offset: null, Target: IRLocal local })
                {
                    assigned.Add(local);
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    CollectAssigned(nested, assigned);
                }
            }
        }

        bool IsInvariant(IRValueInstruction value, HashSet<IRLocal> assigned)
        {
            if (value.ValueType.IsFatPointer || this.Analysis.Placed.Contains(value))
            {
                return false;
            }

            return IsInvariantPart(value, assigned);
        }

        bool IsInvariantPart(IRValueInstruction value, HashSet<IRLocal> assigned)
        {
            switch (value)
            {
                case IRConstantInteger or IRStructProperty:
                    return true;

                case IRLocal local:
                    return !value.ValueType.IsFatPointer && !assigned.Contains(local);

                case IRConvert convert when IRNumeric.IsFloat(convert.Value.ValueType.DataType) && !IRNumeric.IsFloat(convert.ValueType.DataType):
                    return false;

                case IRDivide divide when !IRNumeric.IsFloat(divide.ValueType.DataType):
                    return IRNumeric.TryGetInteger(divide.Right, divide.ValueType.DataType, out long divisor) && divisor != 0 && divisor != -1
                        && this.Analysis.IsUnshared(divide) && IsInvariantPart(divide.Left, assigned) && IsInvariantPart(divide.Right, assigned);

                case IRAdd or IRSubtract or IRMultiply or IRDivide or IRBitwise or IRCompare or IRConvert or IRMath:
                    return this.Analysis.IsUnshared(value) && !this.Analysis.Placed.Contains(value) && IRTree.Children(value).All(c => IsInvariantPart(c, assigned));

                case IRLength { Target: IRLocal array }:
                    return this.Analysis.IsUnshared(value) && !assigned.Contains(array);

                default:
                    return false;
            }
        }
    }
}
