using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Emission
{
    internal class HLSLInstructionEmitter
    {
        StringBuilder Builder { get; set; }

        IRFunction Function { get; set; }

        public HLSLInstructionEmitter(IRFunction function)
        {
            this.Function = function;
            this.Builder = new StringBuilder();
        }

        public List<string> EmitInstructions(List<IRInstruction> instructions)
        {
            return instructions.Select(EmitInstruction).ToList();
        }

        public string EmitInstruction(IRInstruction instruction)
        {
            return instruction switch
            {
                IRAdd add => EmitAdd(add),
                IRSubtract subtract => EmitSubtract(subtract),
                IRMultiply multiply => EmitMultiply(multiply),
                IRDivide divide => EmitDivide(divide),

                IRConstantInteger i => EmitConstant(i),
                IRCall call => EmitCall(call),
                IRBlock block => EmitBlock(block),
                IRReturn ret => EmitReturn(ret),
                IRLoad load => EmitLoad(load),
                IRStore store => EmitStore(store),
                IRCompare compare => EmitCompare(compare),
                IRConditionalBranch conditionalBranch => EmitConditionalBranch(conditionalBranch),
                IRLoop loop => EmitLoop(loop),

                IRConstantString str => EmitLoadString(str),
                IRGlobal global => EmitLoadGlobal(global),
                IRLocal local => EmitLoadLocal(local),
                IRBytes bytes => EmitBytes(bytes),

                IRMalloc malloc => EmitMalloc(malloc),
                IRPanic panic => EmitPanic(panic),

                IRInstantiateArray instantiateArray => EmitInstantiateArray(instantiateArray),
                IRInstantiateStruct instantiateStruct => EmitInstantiateStruct(instantiateStruct),
                IRStructProperty property => EmitProperty(property),
                _ => throw ErrorHandler.CreateNotImplimented($"No HLSL translation implemented for instruction '{instruction.GetType().Name}'")
            };
        }

        private string EmitAdd(IRAdd add)
        {
            return $"({EmitInstruction(add.Left)} + {EmitInstruction(add.Right)})";
        }

        private string EmitSubtract(IRSubtract subtract)
        {
            return $"({EmitInstruction(subtract.Left)} - {EmitInstruction(subtract.Right)})";
        }

        private string EmitMultiply(IRMultiply multiply)
        {
            return $"({EmitInstruction(multiply.Left)} * {EmitInstruction(multiply.Right)})";
        }

        private string EmitDivide(IRDivide divide)
        {
            return $"({EmitInstruction(divide.Left)} / {EmitInstruction(divide.Right)})";
        }

        private string EmitConstant(IRConstantInteger i)
        {
            return i.Value.ToString();
        }

        private string EmitLoadString(IRConstantString str)
        {
            throw ErrorHandler.CreateNotImplimented($"Strings are not supported when emitting HLSL.");
        }

        private string EmitLoadGlobal(IRGlobal global)
        {
            return global.Name;
        }

        private string EmitLoadLocal(IRLocal local)
        {
            return local.Name;
        }

        private string EmitBytes(IRBytes bytes)
        {
            throw ErrorHandler.Create($"'IRBytes' is not supported when emitting HLSL.");
        }

        private string EmitLoad(IRLoad load)
        {
            if (load.Offset != null)
            {
                if(load.Target.ValueType.DataType == IRDataTypes.Array)
                {
                    return $"{EmitInstruction(load.Target)}[{EmitInstruction(load.Offset)}]";
                }
                else
                {
                    return $"{EmitInstruction(load.Target)}.{EmitInstruction(load.Offset)}";
                }
            }
            return EmitInstruction(load.Target);
        }

        private string EmitStore(IRStore store)
        {
            if (store.Offset != null)
            {
                if (store.Target.ValueType.DataType == IRDataTypes.Array)
                {
                    return $"{EmitInstruction(store.Target)}[{EmitInstruction(store.Offset)}] = {EmitInstruction(store.Value)};";
                }
                else
                {
                    return $"{EmitInstruction(store.Target)}.{EmitInstruction(store.Offset)} = {EmitInstruction(store.Value)};";
                }
            }
            return $"{EmitInstruction(store.Target)} = {EmitInstruction(store.Value)};";
        }

        private string EmitMalloc(IRMalloc malloc)
        {
            throw ErrorHandler.CreateNotImplimented($"Cannot create allocations when emitting HLSL.");
        }

        private string EmitInstantiateStruct(IRInstantiateStruct instantiateStruct)
        {
            var arguments = string.Join(", ", instantiateStruct.PropertyValues.Select(EmitInstruction));
            return $"{instantiateStruct.StructSchema.Name}({arguments})";
        }

        private string EmitInstantiateArray(IRInstantiateArray instantiateArray)
        {
            var elements = string.Join(", ", instantiateArray.ElementValues.Select(EmitInstruction));
            return $"{{ {elements} }}";
        }

        private string EmitProperty(IRStructProperty property)
        {
            return property.Name;
        }

        private string EmitCall(IRCall call)
        {
            var arguments = string.Join(", ", call.Arguments.Select(EmitInstruction));
            return $"{call.Function.Name}({arguments})";
        }

        private string EmitBlock(IRBlock block)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"/* {block.Name} */");
            sb.AppendLine("{");
            sb.AppendLine($"\t{string.Join("\n\t", EmitInstructions(block.Instructions))}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string EmitReturn(IRReturn ret)
        {
            if (ret.Values == null || ret.Values.Count == 0)
            {
                return "return;";
            }

            if (ret.Values.Count == 1)
            {
                return $"return {EmitInstruction(ret.Values[0])};";
            }

            var returnValues = string.Join(", ", ret.Values.Select(EmitInstruction));
            return $"return {{ {returnValues} }};";
        }

        private string EmitCompare(IRCompare compare)
        {
            string op = compare.Operator switch
            {
                IRComparisonOperator.EqualTo => "==",
                IRComparisonOperator.NotEqualTo => "!=",
                IRComparisonOperator.LessThan => "<",
                IRComparisonOperator.GreaterThan => ">",
                IRComparisonOperator.LessThanOrEqual => "<=",
                IRComparisonOperator.GreaterThanOrEqual => ">=",
                _ => "=="
            };
            return $"({EmitInstruction(compare.Left)} {op} {EmitInstruction(compare.Right)})";
        }

        private string EmitConditionalBranch(IRConditionalBranch cb)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"if ({EmitInstruction(cb.Condition)})");
            sb.AppendLine("{");
            sb.AppendLine($"    /* {cb.ThenBlock.Name} */");
            sb.AppendLine("}");
            sb.AppendLine("else");
            sb.AppendLine("{");
            sb.AppendLine($"    /* {cb.ElseBlock.Name} */");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string EmitLoop(IRLoop loop)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"while ({EmitInstruction(loop.Condition)})");
            sb.AppendLine("{");
            sb.AppendLine($"    /* {loop.Block.Name} */");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string EmitPanic(IRPanic panic)
        {
            return "discard;";
        }
    }
}
