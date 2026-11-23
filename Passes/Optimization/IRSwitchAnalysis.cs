using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonIR.Passes.Optimization
{
    internal sealed class IRSwitch
    {
        public required IRLocal Subject { get; init; }

        public required long Minimum { get; init; }

        public required int[] Slots { get; init; }

        public required List<IRBlock?> Cases { get; init; }

        public IRBlock? Default { get; init; }

        public required List<IRValueInstruction> Conditions { get; init; }
    }

    internal static class IRSwitchAnalysis
    {
        const int MinimumCases = 4;

        const int MaximumRange = 1024;

        const int SlotsPerCase = 16;

        public static IRSwitch? Find(IRConditionalBranch branch, Func<IRValueInstruction, bool>? usable = null)
        {
            IRLocal? subject = null;
            List<(List<long> Values, IRBlock? Body)> cases = new List<(List<long>, IRBlock?)>();
            List<IRValueInstruction> conditions = new List<IRValueInstruction>();
            IRConditionalBranch? current = branch;
            IRBlock? fallback = null;

            while (current != null)
            {
                if (usable != null && !usable(current.Condition) || !Values(current.Condition, out IRLocal? local, out List<long> values, out bool equal) || subject != null && local != subject)
                {
                    break;
                }

                subject = local;
                conditions.Add(current.Condition);

                IRBlock? body = equal ? current.ThenBlock : current.HasElseBlock ? current.ElseBlock : null;
                IRBlock? rest = equal ? current.HasElseBlock ? current.ElseBlock : null : current.ThenBlock;

                cases.Add((values, body));
                fallback = rest;
                current = rest is { Instructions.Count: 1 } && rest.Instructions[0] is IRConditionalBranch next ? next : null;
            }

            if (subject == null)
            {
                return null;
            }

            List<long> all = cases.SelectMany(c => c.Values).Distinct().ToList();

            if (all.Count < MinimumCases)
            {
                return null;
            }

            long minimum = all.Min();
            long range = all.Max() - minimum + 1;

            if (range > MaximumRange || range > (long)all.Count * SlotsPerCase)
            {
                return null;
            }

            int[] slots = Enumerable.Repeat(-1, (int)range).ToArray();

            for (int i = 0; i < cases.Count; i++)
            {
                foreach (long value in cases[i].Values)
                {
                    if (slots[value - minimum] < 0)
                    {
                        slots[value - minimum] = i;
                    }
                }
            }

            return new IRSwitch
            {
                Subject = subject,
                Minimum = minimum,
                Slots = slots,
                Cases = cases.Select(c => c.Body).ToList(),
                Default = fallback,
                Conditions = conditions,
            };
        }

        static bool Values(IRValueInstruction condition, out IRLocal? local, out List<long> values, out bool equal)
        {
            local = null;
            values = new List<long>();
            equal = true;

            if (condition is IRCompare { Operator: IRComparisonOperator.NotEqualTo } different)
            {
                equal = false;
                return Single(different, ref local, values);
            }

            return Equalities(condition, ref local, values);
        }

        static bool Equalities(IRValueInstruction condition, ref IRLocal? local, List<long> values)
        {
            return condition switch
            {
                IRCompare { Operator: IRComparisonOperator.EqualTo } compare => Single(compare, ref local, values),
                IRBitwise { Operator: IRBitwiseOperator.Or, ValueType.DataType: IRDataTypes.Bool } or => Equalities(or.Left, ref local, values) && Equalities(or.Right, ref local, values),
                _ => false,
            };
        }

        static bool Single(IRCompare compare, ref IRLocal? local, List<long> values)
        {
            if (compare.Left is not IRLocal tested || compare.Right is not IRConstantInteger constant || !IRNumeric.IsInteger(tested.ValueType.DataType)
                || !IRNumeric.IsInteger(constant.IntegerType) || constant.Value != Math.Floor(constant.Value) || Math.Abs(constant.Value) > int.MaxValue
                || local != null && local != tested)
            {
                return false;
            }

            local = tested;
            values.Add((long)constant.Value);
            return true;
        }
    }
}
