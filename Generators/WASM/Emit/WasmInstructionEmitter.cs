using CommonIR.Errors;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.Generators.WASM.Translation;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmInstructionEmitter
    {
        private Stack<IRBlock> BlockContextStack = new Stack<IRBlock>();

        IRFunction Malloc { get; set; }

        IRFunction Free { get; set; }

        public WasmInstructionEmitter(IRFunction malloc, IRFunction free)
        {
            this.Malloc = malloc;
            this.Free = free;
        }

        public List<byte> EmitInstructions(List<IRInstruction> instructions)
        {
            var bytecode = new List<byte>();

            foreach (var instruction in instructions)
            {
                bytecode.AddRange(EmitInstruction(instruction));
            }

            //if (instructions.Last() is not IRReturn)
            //{
            //    bytecode.Add((byte)WasmInstructions.End);
            //}

            return bytecode;
        }

        public byte[] EmitInstruction(IRInstruction instruction)
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

                IRString str => EmitLoadString(str),
                IRGlobal global => EmitLoadGlobal(global),
                IRLocal local => EmitLoadLocal(local),
                IRStruct _struct => EmitStruct(_struct),
                IRBytes bytes => EmitBytes(bytes),

                IRMalloc malloc => EmitMalloc(malloc),

                _ => throw new NotImplementedException($"No Wasm translation implemented for instruction '{instruction.GetType().Name}'")
            };
        }

        public byte[] EmitMalloc(IRMalloc malloc)
        {
            return [.. EmitInstruction(malloc.Bytes), (byte)WasmInstructions.Call, .. LEB128.EncodeUnsigned(this.Malloc.Offset)];
        }

        public byte[] EmitBytes(IRBytes bytes)
        {
            return bytes.Bytes;
        }

        public byte[] EmitStruct(IRStruct _struct)
        {
            // TODO: Impliment struct allocation using the factorized "malloc" function.
            throw ErrorHandler.CreateNotImplimented("");
        }

        public byte[] EmitAdd(IRAdd add)
        {
            return [
                .. EmitInstruction(add.Left),
                .. EmitInstruction(add.Right),
                add.ValueType.DataType switch {
                    IRDataTypes.Int32 => (byte)WasmInstructions.I32_add,
                    IRDataTypes.Int64 => (byte)WasmInstructions.I64_add,
                    IRDataTypes.Float32 => (byte)WasmInstructions.F32_add,
                    IRDataTypes.Float64 => (byte)WasmInstructions.F64_add,
                    _ => throw ErrorHandler.Create($"IRAdd does not support operands of type {add.ValueType.Dump(0)}")
                }
            ];
        }

        public byte[] EmitSubtract(IRSubtract add)
        {
            return [
                .. EmitInstruction(add.Left),
                .. EmitInstruction(add.Right),
                add.ValueType.DataType switch {
                    IRDataTypes.Int32 => (byte)WasmInstructions.I32_sub,
                    IRDataTypes.Int64 => (byte)WasmInstructions.I64_sub,
                    IRDataTypes.Float32 => (byte)WasmInstructions.F32_sub,
                    IRDataTypes.Float64 => (byte)WasmInstructions.F64_sub,
                    _ => throw ErrorHandler.Create($"IRSubtract does not support operands of type {add.ValueType.Dump(0)}")
                }
            ];
        }

        public byte[] EmitMultiply(IRMultiply add)
        {
            return [
                .. EmitInstruction(add.Left),
                .. EmitInstruction(add.Right),
                add.ValueType.DataType switch {
                    IRDataTypes.Int32 => (byte)WasmInstructions.I32_mul,
                    IRDataTypes.Int64 => (byte)WasmInstructions.I64_mul,
                    IRDataTypes.Float32 => (byte)WasmInstructions.F32_mul,
                    IRDataTypes.Float64 => (byte)WasmInstructions.F64_mul,
                    _ => throw ErrorHandler.Create($"IRMultiply does not support operands of type {add.ValueType.Dump(0)}")
                }
            ];
        }

        public byte[] EmitDivide(IRDivide divide)
        {
            return [
               .. EmitInstruction(divide.Left),
                .. EmitInstruction(divide.Right),
                divide.ValueType.DataType switch {
                    IRDataTypes.Int32 => (byte)WasmInstructions.I32_div_s,
                    IRDataTypes.UInt32 => (byte)WasmInstructions.I32_div_u,

                    IRDataTypes.Int64 => (byte)WasmInstructions.I64_div_s,
                    IRDataTypes.UInt64 => (byte)WasmInstructions.I64_div_u,

                    IRDataTypes.Float32 => (byte)WasmInstructions.F32_div,
                    IRDataTypes.Float64 => (byte)WasmInstructions.F64_div,
                    _ => throw ErrorHandler.Create($"IRDivide does not support operands of type {divide.ValueType.Dump(0)}")
                }
           ];
        }

        public byte[] EmitLoadLocal(IRLocal local)
        {
            return [(byte)WasmInstructions.Local_get, .. LEB128.EncodeUnsigned(local.Offset)];
        }

        public byte[] EmitLoadGlobal(IRGlobal global)
        {
            return [(byte)WasmInstructions.Global_get, .. LEB128.EncodeUnsigned(global.Offset)];
        }

        public byte[] EmitLoadString(IRString str)
        {
            List<byte> bytecode = [
                (byte)WasmInstructions.I32_const, .. LEB128.EncodeSigned((int)str.Offset),
                (byte)WasmInstructions.I32_const, .. LEB128.EncodeSigned(Encoding.UTF8.GetBytes(str.Value).LongLength)
            ];

            return bytecode.ToArray();
        }

        public byte[] EmitStore(IRStore store) // TODO: fix this suddenly not emitting instructions for multiply, etc
        {
            List<byte> bytecode = [];


            if(store.Offset != null)
            {
                bytecode.AddRange(EmitInstruction(store.Target));

                bytecode.AddRange(store.Offset switch
                {
                    IRProperty property => [(byte)WasmInstructions.I32_const, .. LEB128.EncodeSigned(property.Offset)],
                    _ => throw ErrorHandler.CreateNotImplimented($"Storing to offset of type '{store.Offset.GetType().FullName}' is not supported")
                });

                bytecode.Add((byte)WasmInstructions.I32_add);
                bytecode.AddRange(EmitInstruction(store.Value));

                bytecode.AddRange([(byte)WasmInstructions.I32_store, .. LEB128.EncodeUnsigned(2), .. LEB128.EncodeUnsigned(0)]);

                return bytecode.ToArray();
            }

            switch (store.Target)
            {
                case IRLocal local:
                    return local.IsMutable ? [.. EmitInstruction(store.Value), (byte)WasmInstructions.Local_set, .. LEB128.EncodeUnsigned(local.Offset)] : throw ErrorHandler.Create($"Cannot emit store on immutable local \"{local.Name}\"");

                case IRGlobal global:
                    return global.IsMutable ? [.. EmitInstruction(store.Value), (byte)WasmInstructions.Global_set, .. LEB128.EncodeUnsigned(global.Offset)] : throw ErrorHandler.Create($"Cannot emit store on immutable global \"{global.Name}\"");

                default:
                    bytecode.AddRange(EmitInstruction(store.Target));
                    break;
            }


            bytecode.Add((byte)WasmInstructions.I32_store);

            return bytecode.ToArray();
        }

        public byte[] EmitLoad(IRLoad load)
        {
            List<byte> bytecode = [];

            if(load.Offset != null)
            {
                bytecode.AddRange(EmitInstruction(load.Target));

                bytecode.AddRange(load.Offset switch
                {
                    IRProperty property => [(byte)WasmInstructions.I32_const, .. LEB128.EncodeSigned(property.Offset)],
                    _ => throw ErrorHandler.CreateNotImplimented($"Loading from offset of type '{load.Offset.GetType().FullName}' is not supported")
                });

                bytecode.Add((byte)WasmInstructions.I32_add);

                bytecode.AddRange([(byte)WasmInstructions.I32_load, .. LEB128.EncodeUnsigned(2), .. LEB128.EncodeUnsigned(0)]);

                return bytecode.ToArray();
            }

            switch(load.Target)
            {
                case IRLocal local:
                    return EmitLoadLocal(local);

                case IRGlobal global:
                    return EmitLoadGlobal(global);

                default:
                    throw ErrorHandler.CreateNotImplimented($"Load targeting \"{load.Target}\" is not yet implimented.");
            }
        }


        public byte[] EmitReturn(IRReturn ret) // Note: Complete proper return handling (as WASM requires leftover value(s) on the stack as the return).
        {
            List<byte> bytes = new List<byte>();

            if(ret.Value != null)
            {
                bytes.AddRange(EmitInstruction(ret.Value));
            }

            return [
                .. bytes, 
                (byte)WasmInstructions.Return
            ];
        }

        public byte[] EmitConstant(IRConstantInteger constInt)
        {
            return [constInt.IntegerType switch 
            {
                IRDataTypes.Int32 => (byte)WasmInstructions.I32_const,
                IRDataTypes.Int64 => (byte)WasmInstructions.I64_const,
                IRDataTypes.Float32 => (byte)WasmInstructions.F32_const,
                IRDataTypes.Float64 => (byte)WasmInstructions.F64_const,

                _ => throw ErrorHandler.Create($"Cannot emit constant of type {constInt.IntegerType}")
            }, .. LEB128.EncodeSigned(constInt.Value)];
        }

        public byte[] EmitCall(IRCall call)
        {
            return [
                .. call.Arguments.SelectMany(i => EmitInstruction(i)),
                (byte)WasmInstructions.Call,
                .. LEB128.EncodeUnsigned(call.Function.Offset)
            ];
        }

        public byte[] EmitBlock(IRBlock block)
        {
            List<byte> bytes = [
                (byte)WasmInstructions.Block,
                (byte)WasmTypeTranslator.TranslateIRType(block.ReturnType)
            ];

            BlockContextStack.Push(block);
            bytes.AddRange(EmitInstructions(block.Instructions));
            BlockContextStack.Pop();

            bytes.Add((byte)WasmInstructions.End);

            return bytes.ToArray();
        }

        public byte[] EmitConditionalBranch(IRConditionalBranch conditionalBranch)
        {
            List<byte> bytes = [];

            bytes.AddRange([
                .. EmitInstruction(conditionalBranch.Condition),
                (byte)WasmInstructions.If,
                (byte)WasmTypeTranslator.TranslateIRType(conditionalBranch.ThenBlock.ReturnType),
                .. EmitInstructions(conditionalBranch.ThenBlock.Instructions),
            ]);

            if (conditionalBranch.HasElseBlock)
            {
                bytes.Add((byte)WasmInstructions.Else);
                bytes.AddRange(EmitInstructions(conditionalBranch.ElseBlock.Instructions));
            }

            bytes.Add((byte)WasmInstructions.End);

            return bytes.ToArray();
        }

        public byte[] EmitCompare(IRCompare compare)
        {
            WasmInstructions comparisonInstruction = (compare.ValueType.DataType, compare.Operator) switch
            {
                (IRDataTypes.Int32, IRComparisonOperator.EqualTo) => WasmInstructions.I32_eq,
                (IRDataTypes.Int32, IRComparisonOperator.NotEqualTo) => WasmInstructions.I32_ne,
                (IRDataTypes.Int32, IRComparisonOperator.LessThan) => WasmInstructions.I32_lt_s,
                (IRDataTypes.Int32, IRComparisonOperator.GreaterThan) => WasmInstructions.I32_gt_s,
                (IRDataTypes.Int32, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.I32_le_s,
                (IRDataTypes.Int32, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.I32_ge_s,

                (IRDataTypes.UInt32, IRComparisonOperator.EqualTo) => WasmInstructions.I32_eq,
                (IRDataTypes.UInt32, IRComparisonOperator.NotEqualTo) => WasmInstructions.I32_ne,
                (IRDataTypes.UInt32, IRComparisonOperator.LessThan) => WasmInstructions.I32_lt_u,
                (IRDataTypes.UInt32, IRComparisonOperator.GreaterThan) => WasmInstructions.I32_gt_u,
                (IRDataTypes.UInt32, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.I32_le_u,
                (IRDataTypes.UInt32, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.I32_ge_u,

                (IRDataTypes.Int64, IRComparisonOperator.EqualTo) => WasmInstructions.I64_eq,
                (IRDataTypes.Int64, IRComparisonOperator.NotEqualTo) => WasmInstructions.I64_ne,
                (IRDataTypes.Int64, IRComparisonOperator.LessThan) => WasmInstructions.I64_lt_s,
                (IRDataTypes.Int64, IRComparisonOperator.GreaterThan) => WasmInstructions.I64_gt_s,
                (IRDataTypes.Int64, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.I64_le_s,
                (IRDataTypes.Int64, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.I64_ge_s,

                (IRDataTypes.UInt64, IRComparisonOperator.EqualTo) => WasmInstructions.I64_eq,
                (IRDataTypes.UInt64, IRComparisonOperator.NotEqualTo) => WasmInstructions.I64_ne,
                (IRDataTypes.UInt64, IRComparisonOperator.LessThan) => WasmInstructions.I64_lt_u,
                (IRDataTypes.UInt64, IRComparisonOperator.GreaterThan) => WasmInstructions.I64_gt_u,
                (IRDataTypes.UInt64, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.I64_le_u,
                (IRDataTypes.UInt64, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.I64_ge_u,

                (IRDataTypes.Float32, IRComparisonOperator.EqualTo) => WasmInstructions.F32_eq,
                (IRDataTypes.Float32, IRComparisonOperator.NotEqualTo) => WasmInstructions.F32_ne,
                (IRDataTypes.Float32, IRComparisonOperator.LessThan) => WasmInstructions.F32_lt,
                (IRDataTypes.Float32, IRComparisonOperator.GreaterThan) => WasmInstructions.F32_gt,
                (IRDataTypes.Float32, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.F32_le,
                (IRDataTypes.Float32, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.F32_ge,

                (IRDataTypes.Float64, IRComparisonOperator.EqualTo) => WasmInstructions.F64_eq,
                (IRDataTypes.Float64, IRComparisonOperator.NotEqualTo) => WasmInstructions.F64_ne,
                (IRDataTypes.Float64, IRComparisonOperator.LessThan) => WasmInstructions.F64_lt,
                (IRDataTypes.Float64, IRComparisonOperator.GreaterThan) => WasmInstructions.F64_gt,
                (IRDataTypes.Float64, IRComparisonOperator.LessThanOrEqual) => WasmInstructions.F64_le,
                (IRDataTypes.Float64, IRComparisonOperator.GreaterThanOrEqual) => WasmInstructions.F64_ge,

                _ => throw ErrorHandler.CreateNotImplimented($"Instruction '{compare.Operator}' for type '{compare.ValueType.DataType}' is not yet implemented.")
            };

            return [
                .. EmitInstruction(compare.Left),
                .. EmitInstruction(compare.Right),
                (byte)comparisonInstruction,
            ];
        }
    }
}
