using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRLocalSimplifier : IRPass
    {
        sealed class Usage
        {
            public List<(IRBlock Block, IRStore Store, int TopLevelIndex)> Writes { get; } = new();

            public List<(IRSlot Slot, int TopLevelIndex)> Reads { get; } = new();

            public bool HasOpaqueRead { get; set; }
        }

        IRModule Module { get; set; }

        IRFunctionAnalysis Analysis { get; set; } = null!;

        Dictionary<IRLocal, Usage> Usages { get; } = new Dictionary<IRLocal, Usage>(ReferenceEqualityComparer.Instance);

        HashSet<IRInstruction> Visited { get; } = new HashSet<IRInstruction>(ReferenceEqualityComparer.Instance);

        public IRLocalSimplifier(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            bool changed = false;

            foreach (IRFunction function in this.Module.Functions)
            {
                this.Analysis = new IRFunctionAnalysis(function);

                if (!this.Analysis.IsSupported)
                {
                    continue;
                }

                this.Usages.Clear();
                this.Visited.Clear();

                for (int i = 0; i < function.Entryblock.Instructions.Count; i++)
                {
                    CollectStatement(function.Entryblock, function.Entryblock.Instructions[i], i);
                }

                HashSet<IRLocal> companions = new HashSet<IRLocal>(
                    function.Locals.Concat(function.Parameters).Select(l => l.LengthCompanion).OfType<IRLocal>(),
                    ReferenceEqualityComparer.Instance);

                List<(IRLocal Local, Usage Usage)> candidates = function.Locals
                    .Where(l => !companions.Contains(l) && this.Usages.ContainsKey(l))
                    .Where(l => l.LengthCompanion == null || !this.Usages.ContainsKey(l.LengthCompanion))
                    .Select(l => (l, this.Usages[l]))
                    .ToList();

                List<Usage> unchanged = new List<Usage>();

                foreach ((IRLocal local, Usage usage) in candidates)
                {
                    if (Propagate(local, usage))
                    {
                        changed = true;
                    }
                    else
                    {
                        unchanged.Add(usage);
                    }
                }

                foreach (Usage usage in unchanged)
                {
                    changed |= RemoveDeadWrites(usage);
                }
            }

            return changed;
        }

        Usage UsageOf(IRLocal local)
        {
            if (!this.Usages.TryGetValue(local, out Usage? usage))
            {
                usage = new Usage();
                this.Usages[local] = usage;
            }

            return usage;
        }

        void CollectBlock(IRBlock block, int topLevelIndex)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                CollectStatement(block, instruction, topLevelIndex);
            }
        }

        void CollectStatement(IRBlock block, IRInstruction statement, int topLevelIndex)
        {
            if (statement is IRStore store && store.Offset == null && store.Target is IRLocal target)
            {
                UsageOf(target).Writes.Add((block, store, topLevelIndex));
            }

            if (this.Visited.Add(statement))
            {
                CollectOperands(statement, topLevelIndex);
            }

            foreach (IRBlock nested in IRTree.NestedBlocks(statement))
            {
                CollectBlock(nested, topLevelIndex);
            }
        }

        void CollectOperands(IRInstruction instruction, int topLevelIndex)
        {
            if (instruction is IRSizeOf sizeOf && sizeOf.Target is IRLocal sized)
            {
                UsageOf(sized).HasOpaqueRead = true;
            }

            foreach (IRSlot slot in IRTree.Slots(instruction))
            {
                IRValueInstruction? value = slot.Value;

                switch (value)
                {
                    case null:
                    case IRLocal when slot.Kind == IRSlotKind.StoreTarget:
                        continue;

                    case IRLocal local:
                        {
                            Usage usage = UsageOf(local);

                            if (slot.Kind == IRSlotKind.Value)
                            {
                                usage.Reads.Add((slot, topLevelIndex));
                            }
                            else
                            {
                                usage.HasOpaqueRead = true;
                            }

                            continue;
                        }
                }

                if (!(IRTree.IsInvocation(value) && this.Analysis.Placed.Contains(value)) && this.Visited.Add(value))
                {
                    CollectOperands(value, topLevelIndex);
                }
            }
        }

        bool IsInEntryBlock(IRBlock block) => ReferenceEquals(this.Analysis.Function.Entryblock, block);

        bool Propagate(IRLocal local, Usage usage)
        {
            IRDataTypes type = local.ValueType.DataType;

            if (!IRNumeric.IsNumeric(type) || usage.HasOpaqueRead || usage.Writes.Count != 1 || usage.Reads.Count == 0)
            {
                return false;
            }

            (IRBlock block, IRStore store, int index) = usage.Writes[0];

            if (!IsInEntryBlock(block) || index >= block.Instructions.Count || !ReferenceEquals(block.Instructions[index], store) || store.Value is not IRConstantInteger constant
                || constant.IntegerType != type || usage.Reads.Any(r => r.TopLevelIndex <= index))
            {
                return false;
            }

            if (IRNumeric.IsFloat(type) ? !IRNumeric.TryGetFloat(constant, type, out _) : !IRNumeric.TryGetInteger(constant, type, out _))
            {
                return false;
            }

            foreach ((IRSlot slot, int _) in usage.Reads)
            {
                slot.Replace(new IRConstantInteger(constant.IntegerType, constant.Value));
            }

            return true;
        }

        bool RemoveDeadWrites(Usage usage)
        {
            if (usage.HasOpaqueRead || usage.Reads.Count > 0)
            {
                return false;
            }

            bool changed = false;

            foreach ((IRBlock block, IRStore store, int _) in usage.Writes)
            {
                if (!IRTree.IsPure(store.Value, this.Analysis))
                {
                    continue;
                }

                int position = block.Instructions.FindIndex(i => ReferenceEquals(i, store));

                if (position < 0)
                {
                    continue;
                }

                block.Instructions.RemoveAt(position);
                IRTree.ReleaseOperands(store, this.Analysis);
                changed = true;
            }

            return changed;
        }
    }
}
