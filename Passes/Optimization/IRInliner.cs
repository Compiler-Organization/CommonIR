using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRInliner : IRPass
    {
        public const int MaximumSize = 24;

        IRModule Module { get; set; }

        int Count { get; set; }

        public IRInliner(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            Dictionary<IRFunction, IRValueInstruction> inlinable = new Dictionary<IRFunction, IRValueInstruction>(ReferenceEqualityComparer.Instance);

            foreach (IRFunction function in this.Module.Functions)
            {
                if (Body(function) is IRValueInstruction body)
                {
                    inlinable[function] = body;
                }
            }

            if (inlinable.Count == 0)
            {
                return false;
            }

            bool changed = false;

            foreach (IRFunction function in this.Module.Functions)
            {
                if (!function.IsKernel)
                {
                    changed |= InlineBlock(function, function.Entryblock, inlinable);
                }
            }

            return changed;
        }

        static bool IsScalar(IRType type) => !type.IsFatPointer && IRNumeric.IsNumeric(type.DataType);

        static IRValueInstruction? Body(IRFunction function)
        {
            if (function.IsKernel || function.ReturnTypes.Count != 1 || !IsScalar(function.ReturnTypes[0]) || function.Locals.Count > 0
                || !function.Parameters.All(p => IsScalar(p.ValueType)) || function.Entryblock is not { Instructions: [IRReturn { Values: [IRValueInstruction value] }] }
                || value.ValueType.DataType != function.ReturnTypes[0].DataType)
            {
                return null;
            }

            int size = 0;
            return IsCopyable(value, function, ref size) ? value : null;
        }

        static bool IsCopyable(IRValueInstruction value, IRFunction function, ref int size)
        {
            if (++size > MaximumSize)
            {
                return false;
            }

            switch (value)
            {
                case IRLocal local:
                    return function.Parameters.Contains(local);

                case IRConstantInteger:
                    return true;

                case IRAdd or IRSubtract or IRMultiply or IRDivide or IRCompare:
                    foreach (IRValueInstruction child in IRTree.Children(value))
                    {
                        if (!IsCopyable(child, function, ref size))
                        {
                            return false;
                        }
                    }
                    return true;

                case IRBitwise bitwise:
                    return IsCopyable(bitwise.Left, function, ref size) && IsCopyable(bitwise.Right, function, ref size);

                case IRConvert convert:
                    return IsScalar(convert.ValueType) && IsScalar(convert.Value.ValueType) && IsCopyable(convert.Value, function, ref size);

                case IRMath math:
                    return IsCopyable(math.Value, function, ref size);

                default:
                    return false;
            }
        }

        static IRValueInstruction Copy(IRValueInstruction value, Dictionary<IRLocal, IRLocal> parameters) => value switch
        {
            IRLocal local => parameters[local],
            IRConstantInteger constant => new IRConstantInteger(constant.IntegerType, constant.Value),
            IRAdd add => new IRAdd(Copy(add.Left, parameters), Copy(add.Right, parameters)),
            IRSubtract subtract => new IRSubtract(Copy(subtract.Left, parameters), Copy(subtract.Right, parameters)),
            IRMultiply multiply => new IRMultiply(Copy(multiply.Left, parameters), Copy(multiply.Right, parameters)),
            IRDivide divide => new IRDivide(Copy(divide.Left, parameters), Copy(divide.Right, parameters)),
            IRCompare compare => new IRCompare(compare.Operator, Copy(compare.Left, parameters), Copy(compare.Right, parameters)),
            IRBitwise bitwise => new IRBitwise(bitwise.Operator, Copy(bitwise.Left, parameters), Copy(bitwise.Right, parameters)),
            IRConvert convert => new IRConvert(Copy(convert.Value, parameters), convert.ValueType),
            IRMath math => new IRMath(math.Operation, Copy(math.Value, parameters), math.ValueType),
            _ => throw new InvalidOperationException($"'{value.GetType().Name}' cannot be inlined."),
        };

        bool InlineBlock(IRFunction caller, IRBlock block, Dictionary<IRFunction, IRValueInstruction> inlinable)
        {
            bool changed = false;

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                IRInstruction instruction = block.Instructions[i];

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    changed |= InlineBlock(caller, nested, inlinable);
                }

                if (instruction is not IRStore { Offset: null, Target: IRLocal target, Value: IRCall call } store
                    || call.Function == caller || !inlinable.TryGetValue(call.Function, out IRValueInstruction? body)
                    || call.Arguments.Count != call.Function.Parameters.Count || target.ValueType.DataType != body.ValueType.DataType)
                {
                    continue;
                }

                List<IRInstruction> replacement = new List<IRInstruction>();
                Dictionary<IRLocal, IRLocal> parameters = new Dictionary<IRLocal, IRLocal>(ReferenceEqualityComparer.Instance);

                for (int a = 0; a < call.Arguments.Count; a++)
                {
                    IRLocal parameter = call.Function.Parameters[a];
                    IRValueInstruction argument = call.Arguments[a];
                    argument.References.Remove(call);

                    IRLocal copy = caller.CreateLocal($"$inline{this.Count++}", parameter.ValueType, true);
                    replacement.Add(new IRStore(copy, argument) { Parent = block });
                    parameters[parameter] = copy;
                }

                call.Function.References.Remove(call);
                target.References.Remove(store);

                replacement.Add(new IRStore(target, Copy(body, parameters)) { Parent = block });

                block.Instructions.RemoveAt(i);
                block.Instructions.InsertRange(i, replacement);
                i += replacement.Count - 1;
                changed = true;
            }

            return changed;
        }
    }
}
