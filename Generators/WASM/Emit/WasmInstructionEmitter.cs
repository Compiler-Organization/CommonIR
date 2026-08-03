using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Translation;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmInstructionEmitter
    {
        private Stack<IRBlock> BlockContextStack = new Stack<IRBlock>();

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
                IRConstantInteger i => EmitConstant(i),
                IRAdd add => EmitAdd(add),
                IRCall call => EmitCall(call),
                IRBlock block => EmitBlock(block),
                IRJump jump => EmitJump(jump),
                IRReturn ret => EmitReturn(ret),
                IRLoad load => EmitLoad(load),

                _ => throw new NotImplementedException($"No Wasm translation implemented for instruction '{instruction.GetType().Name}'")
            };
        }

        public byte[] EmitLoad(IRLoad load)
        {
            List<byte> bytecode = [
                (byte)WasmInstructions.Local_get
            ];

            bytecode.AddRange(load.Target switch
            {
                IRLocal local => LEB128.EncodeUnsigned(local.Offset),
                IRGlobal global => LEB128.EncodeUnsigned(global.Offset),

                _ => throw new NotImplementedException($"No Wasm translation implemented for load target '{load.Target.GetType().Name}'")
            });

            return bytecode.ToArray();
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
            => [
                   (byte)WasmInstructions.I32_const, .. LEB128.EncodeSigned(constInt.Value)
               ];

        public byte[] EmitAdd(IRAdd add)
        {
            return [
                .. EmitInstruction(add.Left),
                .. EmitInstruction(add.Right),
                (byte)WasmInstructions.I32_add
            ];
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

            return bytes.ToArray();
        }

        public byte[] EmitJump(IRJump jump)
        {
            var activeBlocks = BlockContextStack.ToArray();
            int depth = Array.IndexOf(activeBlocks, jump.TargetBlock);

            if (depth == -1)
            {
                throw new InvalidOperationException($"Compilation Error: Target block '{jump.TargetBlock.Name}' is unreachable.");
            }

            return [
                (byte)WasmInstructions.Br,
            .. LEB128.EncodeUnsigned((uint)depth)
            ];
        }

        private bool IsFreestandingExpression(IRInstruction instr)
        {
            return instr is IRConstantInteger;
        }
    }
}
