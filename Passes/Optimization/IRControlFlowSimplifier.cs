using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRControlFlowSimplifier : IRPass
    {
        IRModule Module { get; set; }

        IRFunctionAnalysis Analysis { get; set; } = null!;

        bool Changed { get; set; }

        public IRControlFlowSimplifier(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            this.Changed = false;

            foreach (IRFunction function in this.Module.Functions)
            {
                this.Analysis = new IRFunctionAnalysis(function);

                if (this.Analysis.IsSupported)
                {
                    SimplifyBlock(function.Entryblock);
                }
            }

            return this.Changed;
        }

        static bool IsConstantCondition(IRValueInstruction condition, out bool value)
        {
            bool constant = IRNumeric.TryGetInteger(condition, IRDataTypes.Bool, out long bits);
            value = bits != 0;
            return constant;
        }

        static bool IsEmpty(IRBlock? block) => block == null || block.Instructions.Count == 0;

        void SimplifyBlock(IRBlock block)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                IRInstruction instruction = block.Instructions[i];

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    SimplifyBlock(nested);
                }

                if (SimplifyStatement(block, i, instruction))
                {
                    this.Changed = true;
                    i--;
                    continue;
                }

                if (i < block.Instructions.Count - 1 && (IRTree.IsTerminator(instruction) || ExitsOnBothSides(instruction)))
                {
                    List<IRInstruction> unreachable = block.Instructions.GetRange(i + 1, block.Instructions.Count - i - 1);

                    if (CanRemove(unreachable))
                    {
                        Remove(unreachable);
                        block.Instructions.RemoveRange(i + 1, unreachable.Count);
                        this.Changed = true;
                    }
                }
            }
        }

        static bool ExitsOnBothSides(IRInstruction instruction)
            => instruction is IRConditionalBranch branch && branch.HasElseBlock && branch.ElseBlock != null
                && IRTree.AlwaysExits(branch.ThenBlock) && IRTree.AlwaysExits(branch.ElseBlock);

        bool SimplifyStatement(IRBlock block, int index, IRInstruction instruction)
        {
            switch (instruction)
            {
                case IRConditionalBranch branch when IsConstantCondition(branch.Condition, out bool taken):
                    {
                        IRBlock? chosen = taken ? branch.ThenBlock : (branch.HasElseBlock ? branch.ElseBlock : null);
                        IRBlock? dropped = taken ? (branch.HasElseBlock ? branch.ElseBlock : null) : branch.ThenBlock;

                        if (dropped != null && !CanRemove(dropped.Instructions))
                        {
                            return false;
                        }

                        if (dropped != null)
                        {
                            Remove(dropped.Instructions);
                        }

                        IRTree.ReleaseOperands(branch, this.Analysis);
                        block.Instructions.RemoveAt(index);

                        if (chosen != null)
                        {
                            foreach (IRInstruction moved in chosen.Instructions)
                            {
                                moved.Parent = block;
                            }

                            block.Instructions.InsertRange(index, chosen.Instructions);
                            chosen.Instructions = new List<IRInstruction>();
                        }

                        return true;
                    }

                case IRConditionalBranch branch when IsEmpty(branch.ThenBlock) && (!branch.HasElseBlock || IsEmpty(branch.ElseBlock))
                    && IRTree.IsPure(branch.Condition, this.Analysis):
                    IRTree.ReleaseOperands(branch, this.Analysis);
                    block.Instructions.RemoveAt(index);
                    return true;

                case IRLoop loop when IsConstantCondition(loop.Condition, out bool runs) && !runs:
                    {
                        List<IRInstruction> body = [.. loop.Block.Instructions, .. loop.Increment?.Instructions ?? []];

                        if (!CanRemove(body))
                        {
                            return false;
                        }

                        Remove(body);
                        IRTree.ReleaseOperands(loop, this.Analysis);
                        block.Instructions.RemoveAt(index);
                        return true;
                    }

                case IRStore store when store.Offset == null && store.Target is IRLocal or IRGlobal && ReferenceEquals(store.Target, store.Value):
                    IRTree.ReleaseOperands(store, this.Analysis);
                    block.Instructions.RemoveAt(index);
                    return true;

                case IRValueInstruction value when !IRTree.IsInvocation(value) && this.Analysis.UsesOf(value) == 0 && IRTree.IsPure(value, this.Analysis):
                    IRTree.ReleaseOperands(value, this.Analysis);
                    block.Instructions.RemoveAt(index);
                    return true;
            }

            if (instruction is IRConditionalBranch conditional)
            {
                if (conditional.HasElseBlock && IsEmpty(conditional.ElseBlock))
                {
                    conditional.HasElseBlock = false;
                    conditional.ElseBlock = null!;
                    this.Changed = true;
                }
                else if (conditional.HasElseBlock && IsEmpty(conditional.ThenBlock) && conditional.Condition is IRCompare compare
                    && this.Analysis.IsUnshared(compare) && IRExpressionSimplifier.IsInvertible(compare))
                {
                    compare.Operator = IRExpressionSimplifier.Invert(compare.Operator);
                    conditional.ThenBlock = conditional.ElseBlock;
                    conditional.ElseBlock = null!;
                    conditional.HasElseBlock = false;
                    this.Changed = true;
                }
            }

            return false;
        }

        bool CanRemove(List<IRInstruction> statements)
        {
            IRUseCounter inside = new IRUseCounter(this.Analysis.Placed);
            inside.CountStatements(statements);

            return inside.Produced.All(v => this.Analysis.UsesOf(v) == inside.Uses.GetValueOrDefault(v));
        }

        void Remove(List<IRInstruction> statements)
        {
            foreach (IRInstruction statement in statements)
            {
                IRTree.ReleaseStatement(statement, this.Analysis);
            }
        }
    }
}
