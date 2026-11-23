using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Emission
{
    internal class HLSLInstructionEmitter
    {
        public const string FaultFunction = "__fault";

        public const string FaultBuffer = "__faults";

        public const string LoadOutputFunction = "__load_output";

        public const string StoreOutputFunction = "__store_output";

        public static string LoadInputFunction(int input) => $"__load_input{input}";

        public static string StoreInputFunction(int input) => $"__store_input{input}";

        IRLocal? Output => this.Function.KernelOutputOrNull;

        IRFunction Function { get; set; }

        HLSLKernelLayout Layout { get; set; }

        Dictionary<IRLocal, string> Names { get; set; } = new Dictionary<IRLocal, string>();

        HashSet<string> UsedNames { get; set; } = new HashSet<string>(StringComparer.Ordinal);

        Stack<IRBlock?> LoopIncrements { get; set; } = new Stack<IRBlock?>();

        public HLSLInstructionEmitter(IRFunction function, HLSLKernelLayout layout)
        {
            this.Function = function;
            this.Layout = layout;
        }

        public string NameOf(IRLocal local)
        {
            if (this.Names.TryGetValue(local, out string? name))
            {
                return name;
            }

            StringBuilder sanitized = new StringBuilder("v_");

            foreach (char c in local.Name ?? "local")
            {
                sanitized.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
            }

            name = sanitized.ToString();

            for (int suffix = 1; !this.UsedNames.Add(name); suffix++)
            {
                name = $"{sanitized}_{suffix}";
            }

            this.Names[local] = name;
            return name;
        }

        public static string TypeName(IRType type) => type.DataType switch
        {
            IRDataTypes.Int32 => "int",
            IRDataTypes.UInt32 => "uint",
            IRDataTypes.Float32 => "float",
            IRDataTypes.Float64 => "double",
            IRDataTypes.Bool => "bool",
            _ => throw ErrorHandler.CreateNotImplimented($"Values of type '{type.Dump(0)}' are not supported when emitting HLSL.")
        };

        public static string DefaultValue(IRType type) => type.DataType switch
        {
            IRDataTypes.Bool => "false",
            _ => $"({TypeName(type)})0"
        };

        static string Indent(int indentation) => new string(' ', indentation * 4);

        #region Statements

        public void EmitBlock(StringBuilder builder, IRBlock block, int indentation)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                EmitStatement(builder, instruction, indentation);
            }
        }

        void EmitStatement(StringBuilder builder, IRInstruction instruction, int indentation)
        {
            string indent = Indent(indentation);

            switch (instruction)
            {
                case IRStore store:
                    builder.AppendLine($"{indent}{EmitStoreTarget(store)} = {EmitExpression(store.Value)};");
                    break;

                case IRStoreElement storeElement:
                    {
                        if (storeElement.Target is not IRLocal target || (target != this.Output && !this.Layout.Inputs.Contains(target)))
                        {
                            throw ErrorHandler.CreateNotImplimented("Only the kernel's arrays and output buffer can be written to when emitting HLSL.");
                        }

                        string function = target == this.Output ? StoreOutputFunction : StoreInputFunction(this.Layout.Inputs.IndexOf(target));
                        builder.AppendLine($"{indent}{function}({EmitIndex(storeElement.Index)}, {EmitExpression(storeElement.Value)});");
                    }
                    break;

                case IRConditionalBranch conditionalBranch:
                    builder.AppendLine($"{indent}if ({EmitExpression(conditionalBranch.Condition)})");
                    builder.AppendLine($"{indent}{{");
                    EmitBlock(builder, conditionalBranch.ThenBlock, indentation + 1);
                    builder.AppendLine($"{indent}}}");

                    if (conditionalBranch.HasElseBlock)
                    {
                        builder.AppendLine($"{indent}else");
                        builder.AppendLine($"{indent}{{");
                        EmitBlock(builder, conditionalBranch.ElseBlock, indentation + 1);
                        builder.AppendLine($"{indent}}}");
                    }
                    break;

                case IRLoop loop:
                    builder.AppendLine($"{indent}[loop] while ({EmitExpression(loop.Condition)})");
                    builder.AppendLine($"{indent}{{");

                    this.LoopIncrements.Push(loop.Increment);
                    EmitBlock(builder, loop.Block, indentation + 1);
                    this.LoopIncrements.Pop();

                    if (loop.Increment != null)
                    {
                        EmitBlock(builder, loop.Increment, indentation + 1);
                    }

                    builder.AppendLine($"{indent}}}");
                    break;

                case IRBreak:
                    builder.AppendLine($"{indent}break;");
                    break;

                case IRContinue:
                    {
                        if (this.LoopIncrements.Count == 0)
                        {
                            throw ErrorHandler.Create("'continue' used outside of a loop.");
                        }

                        IRBlock? increment = this.LoopIncrements.Peek();

                        if (increment != null)
                        {
                            EmitBlock(builder, increment, indentation);
                        }

                        builder.AppendLine($"{indent}continue;");
                    }
                    break;

                case IRReturn returnInstruction:
                    if (returnInstruction.Values != null && returnInstruction.Values.Count > 0)
                    {
                        throw ErrorHandler.CreateNotImplimented("Kernels cannot return values when emitting HLSL. Write results to the output buffer instead.");
                    }

                    builder.AppendLine($"{indent}return;");
                    break;

                case IRBlock block:
                    builder.AppendLine($"{indent}{{");
                    EmitBlock(builder, block, indentation + 1);
                    builder.AppendLine($"{indent}}}");
                    break;

                case IRCall { Function.GpuIntrinsic: not null } call:
                    builder.AppendLine($"{indent}{EmitIntrinsic(call)};");
                    break;

                case IRCall or IRCallIndirect or IRDispatch or IRAwait:
                    throw ErrorHandler.CreateNotImplimented("Kernels cannot call functions when emitting HLSL.");

                case IRPanic:
                    throw ErrorHandler.CreateNotImplimented("Kernels cannot panic when emitting HLSL.");

                default:
                    throw ErrorHandler.CreateNotImplimented($"No HLSL translation implemented for instruction '{instruction.GetType().Name}'");
            }
        }

        string EmitStoreTarget(IRStore store)
        {
            if (store.Offset != null || store.Target is not IRLocal local)
            {
                throw ErrorHandler.CreateNotImplimented("Only stores to locals can be emitted as HLSL.");
            }

            if (this.Layout.Inputs.Contains(local) || local == this.Output)
            {
                throw ErrorHandler.CreateNotImplimented($"The buffer '{local.Name}' cannot be reassigned on the GPU.");
            }

            return NameOf(local);
        }

        #endregion

        #region Expressions

        string EmitIndex(IRValueInstruction index)
        {
            return index.ValueType.DataType == IRDataTypes.UInt32 ? EmitExpression(index) : $"(uint){EmitExpression(index)}";
        }

        public string EmitExpression(IRValueInstruction value)
        {
            return value switch
            {
                IRConstantInteger constant => EmitConstant(constant),
                IRLocal local => EmitLocal(local),
                IRLoad { Offset: null, Target: IRLocal local } => EmitLocal(local),

                IRAdd add => $"({EmitExpression(add.Left)} + {EmitExpression(add.Right)})",
                IRSubtract subtract => $"({EmitExpression(subtract.Left)} - {EmitExpression(subtract.Right)})",
                IRMultiply multiply => $"({EmitExpression(multiply.Left)} * {EmitExpression(multiply.Right)})",
                IRDivide divide => $"({EmitExpression(divide.Left)} / {EmitExpression(divide.Right)})",
                IRBitwise bitwise => EmitBitwise(bitwise),
                IRCompare compare => EmitCompare(compare),
                IRConvert convert => EmitConvert(convert),
                IRMath { Operation: IRMathOperation.SquareRoot } math => $"sqrt({EmitExpression(math.Value)})",
                IRMath { Operation: IRMathOperation.Reinterpret, ValueType.DataType: IRDataTypes.UInt32 or IRDataTypes.Int32, Value.ValueType.DataType: IRDataTypes.Float32 } math => $"asuint({EmitExpression(math.Value)})",
                IRMath { Operation: IRMathOperation.Reinterpret, ValueType.DataType: IRDataTypes.Float32, Value.ValueType.DataType: IRDataTypes.UInt32 or IRDataTypes.Int32 } math => $"asfloat({EmitExpression(math.Value)})",

                IRLoadElement loadElement => EmitLoadElement(loadElement),
                IRLength length => EmitLength(length),

                IRCall { Function.GpuIntrinsic: not null } call => EmitIntrinsic(call),
                IRCall or IRCallIndirect or IRDispatch or IRAwait => throw ErrorHandler.CreateNotImplimented("Kernels cannot call functions when emitting HLSL."),
                IRConstantString => throw ErrorHandler.CreateNotImplimented("Strings are not supported when emitting HLSL."),
                IRInstantiateArray or IRInstantiateStruct or IRMalloc => throw ErrorHandler.CreateNotImplimented("Kernels cannot allocate memory when emitting HLSL."),

                _ => throw ErrorHandler.CreateNotImplimented($"No HLSL translation implemented for instruction '{value.GetType().Name}'")
            };
        }

        string EmitIntrinsic(IRCall call)
        {
            return $"{call.Function.GpuIntrinsic}({string.Join(", ", call.Arguments.Select(EmitExpression))})";
        }

        string EmitLocal(IRLocal local)
        {
            if (local.ValueType.DataType == IRDataTypes.Array && !this.Layout.Inputs.Contains(local) && local != this.Output)
            {
                throw ErrorHandler.CreateNotImplimented($"Array '{local.Name}' cannot be used on the GPU. Only the kernel's input arrays and output buffer are available.");
            }

            return NameOf(local);
        }

        static string EmitConstant(IRConstantInteger constant)
        {
            switch (constant.IntegerType)
            {
                case IRDataTypes.Bool:
                    return constant.Value != 0 ? "true" : "false";

                case IRDataTypes.Int32:
                    {
                        int value = (int)constant.Value;
                        return value == int.MinValue ? "(-2147483647 - 1)" : value < 0 ? $"({value.ToString(CultureInfo.InvariantCulture)})" : value.ToString(CultureInfo.InvariantCulture);
                    }

                case IRDataTypes.UInt32:
                    return $"{((uint)(long)constant.Value).ToString(CultureInfo.InvariantCulture)}u";

                case IRDataTypes.Float32:
                    {
                        float value = (float)constant.Value;

                        if (float.IsNaN(value))
                        {
                            return "asfloat(0x7FC00000u)";
                        }

                        if (float.IsInfinity(value))
                        {
                            return value > 0 ? "asfloat(0x7F800000u)" : "asfloat(0xFF800000u)";
                        }

                        string text = value.ToString("R", CultureInfo.InvariantCulture);

                        if (!text.Contains('.') && !text.Contains('E'))
                        {
                            text += ".0";
                        }

                        return value < 0 ? $"({text}f)" : $"{text}f";
                    }

                case IRDataTypes.Float64:
                    {
                        string text = constant.Value.ToString("R", CultureInfo.InvariantCulture);

                        if (!text.Contains('.') && !text.Contains('E'))
                        {
                            text += ".0";
                        }

                        return $"((double){text}L)";
                    }

                default:
                    throw ErrorHandler.CreateNotImplimented($"Constants of type '{constant.IntegerType}' are not supported when emitting HLSL.");
            }
        }

        string EmitBitwise(IRBitwise bitwise)
        {
            string op = bitwise.Operator switch
            {
                IRBitwiseOperator.And => "&",
                IRBitwiseOperator.Or => "|",
                IRBitwiseOperator.Xor => "^",
                IRBitwiseOperator.ShiftLeft => "<<",
                IRBitwiseOperator.ShiftRight => ">>",
                _ => throw ErrorHandler.CreateNotImplimented($"Operator '{bitwise.Operator}' is not supported when emitting HLSL.")
            };

            return $"({EmitExpression(bitwise.Left)} {op} {EmitExpression(bitwise.Right)})";
        }

        string EmitCompare(IRCompare compare)
        {
            string op = compare.Operator switch
            {
                IRComparisonOperator.EqualTo => "==",
                IRComparisonOperator.NotEqualTo => "!=",
                IRComparisonOperator.LessThan => "<",
                IRComparisonOperator.GreaterThan => ">",
                IRComparisonOperator.LessThanOrEqual => "<=",
                IRComparisonOperator.GreaterThanOrEqual => ">=",
                _ => throw ErrorHandler.CreateNotImplimented($"Operator '{compare.Operator}' is not supported when emitting HLSL.")
            };

            return $"({EmitExpression(compare.Left)} {op} {EmitExpression(compare.Right)})";
        }

        string EmitConvert(IRConvert convert)
        {
            string value = EmitExpression(convert.Value);

            if (convert.ValueType.DataType == IRDataTypes.Bool && convert.Value.ValueType.DataType != IRDataTypes.Bool)
            {
                return $"({value} != 0)";
            }

            return $"(({TypeName(convert.ValueType)}){value})";
        }

        string EmitLoadElement(IRLoadElement loadElement)
        {
            if (loadElement.Target is not IRLocal target || (!this.Layout.Inputs.Contains(target) && target != this.Output))
            {
                throw ErrorHandler.CreateNotImplimented("Only the kernel's input arrays and output buffer can be indexed when emitting HLSL.");
            }

            string function = target == this.Output ? LoadOutputFunction : LoadInputFunction(this.Layout.Inputs.IndexOf(target));
            return $"{function}({EmitIndex(loadElement.Index)})";
        }

        string EmitLength(IRLength length)
        {
            if (length.Target is IRLocal target)
            {
                if (target == this.Output)
                {
                    return "(int)__count";
                }

                if (this.Layout.Inputs.Contains(target))
                {
                    return $"(int)__length{this.Layout.Inputs.IndexOf(target)}";
                }
            }

            throw ErrorHandler.CreateNotImplimented("Only the lengths of the kernel's input arrays and output buffer are available when emitting HLSL.");
        }

        #endregion
    }
}
