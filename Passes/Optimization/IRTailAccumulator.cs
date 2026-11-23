using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRTailAccumulator : IRPass
    {
        readonly IRModule Module;

        public IRTailAccumulator(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            bool changed = false;

            foreach (IRFunction function in this.Module.Functions.ToList())
            {
                changed |= Transform(function);
            }

            return changed;
        }

        static bool IsScalar(IRType type) => !type.IsFatPointer && IRNumeric.IsNumeric(type.DataType);

        bool Transform(IRFunction function)
        {
            if (function.ReturnTypes.Count != 1 || function.Entryblock == null)
            {
                return false;
            }

            IRType resultType = function.ReturnTypes[0];

            if (resultType.IsFatPointer || !IRNumeric.IsInteger(resultType.DataType)
                || !function.Parameters.All(p => IsScalar(p.ValueType)) || !function.Locals.All(l => IsScalar(l.ValueType)))
            {
                return false;
            }

            List<IRInstruction> body = function.Entryblock.Instructions;

            if (body.Count < 2 || body[^1] is not IRReturn { Values: [IRAdd add] } tail || body[^2] is not IRStore { Offset: null, Target: IRLocal result, Value: IRCall call } callStore
                || call.Function != function || call.Arguments.Count != function.Parameters.Count || add.ValueType.DataType != resultType.DataType)
            {
                return false;
            }

            IRValueInstruction other;

            if (ReferenceEquals(add.Right, result) && !ReferenceEquals(add.Left, result))
            {
                other = add.Left;
            }
            else if (ReferenceEquals(add.Left, result) && !ReferenceEquals(add.Right, result))
            {
                other = add.Right;
            }
            else
            {
                return false;
            }

            if (!IsQuiet(other) || CountUses(function.Entryblock, result) != 2 || !AllowedStatements(function.Entryblock))
            {
                return false;
            }

            IRLocal accumulator = function.CreateLocal("$accumulator", resultType, true);

            foreach (IRReturn returned in Returns(function.Entryblock))
            {
                if (ReferenceEquals(returned, tail) || returned.Values is not [IRValueInstruction value])
                {
                    continue;
                }

                value.References.Remove(returned);
                IRAdd sum = new IRAdd(accumulator, value);
                sum.References.Add(returned);
                returned.Values[0] = sum;
                returned.Operands = [sum];
            }

            body.RemoveAt(body.Count - 1);
            body.RemoveAt(body.Count - 1);

            List<IRInstruction> step = new List<IRInstruction>();
            List<IRLocal> next = new List<IRLocal>();

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                IRValueInstruction argument = call.Arguments[i];
                argument.References.Remove(call);

                IRLocal temporary = function.CreateLocal($"$next{i}", function.Parameters[i].ValueType, true);
                step.Add(new IRStore(temporary, argument));
                next.Add(temporary);
            }

            other.References.Remove(add);
            step.Add(new IRStore(accumulator, new IRAdd(accumulator, other)));

            for (int i = 0; i < next.Count; i++)
            {
                function.Parameters[i].IsMutable = true;
                step.Add(new IRStore(function.Parameters[i], next[i]));
            }

            step.Add(new IRContinue());

            IRBlock loopBody = function.CreateBlock("tail.loop");
            loopBody.Instructions.AddRange(body);
            loopBody.Instructions.AddRange(step);

            foreach (IRInstruction instruction in loopBody.Instructions)
            {
                instruction.Parent = loopBody;
            }

            IRLoop loop = new IRLoop(new IRConstantInteger(IRDataTypes.Bool, 1), loopBody);
            loopBody.Parent = loop;

            IRStore start = new IRStore(accumulator, new IRConstantInteger(resultType.DataType, 0));
            IRReturn unreachable = new IRReturn([accumulator]);

            body.Clear();
            body.Add(start);
            body.Add(loop);
            body.Add(unreachable);

            start.Parent = function.Entryblock;
            loop.Parent = function.Entryblock;
            unreachable.Parent = function.Entryblock;

            return true;
        }

        static bool IsQuiet(IRValueInstruction value) => value switch
        {
            IRLocal or IRConstantInteger => true,
            IRAdd or IRSubtract or IRMultiply or IRCompare => IRTree.Children(value).All(IsQuiet),
            IRBitwise bitwise => IsQuiet(bitwise.Left) && IsQuiet(bitwise.Right),
            IRConvert convert => IRNumeric.IsIntegral(convert.ValueType.DataType) && IRNumeric.IsIntegral(convert.Value.ValueType.DataType) && IsQuiet(convert.Value),
            _ => false,
        };

        static bool AllowedStatements(IRBlock block, bool inLoop = false)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                if (instruction is IRFree or IRAwait or IRBytes || (!inLoop && instruction is IRBreak or IRContinue))
                {
                    return false;
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    if (!AllowedStatements(nested, inLoop || instruction is IRLoop))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static IEnumerable<IRReturn> Returns(IRBlock block)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                if (instruction is IRReturn returned)
                {
                    yield return returned;
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    foreach (IRReturn inner in Returns(nested))
                    {
                        yield return inner;
                    }
                }
            }
        }

        static int CountUses(IRBlock block, IRLocal local)
        {
            int count = 0;

            void Visit(IRInstruction instruction)
            {
                foreach (IRSlot slot in IRTree.Slots(instruction))
                {
                    if (slot.Value is IRValueInstruction value)
                    {
                        if (ReferenceEquals(value, local))
                        {
                            count++;
                        }
                        else
                        {
                            Visit(value);
                        }
                    }
                }

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    foreach (IRInstruction inner in nested.Instructions)
                    {
                        Visit(inner);
                    }
                }
            }

            foreach (IRInstruction instruction in block.Instructions)
            {
                Visit(instruction);
            }

            return count;
        }
    }
}
