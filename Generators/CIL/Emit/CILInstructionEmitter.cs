using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.Generators.CIL.Emit
{
    internal class CILInstructionEmitter
    {
        ILGenerator ILEmitter { get; set; }

        IRFunction Function { get; set; }

        public CILInstructionEmitter(IRFunction function, ILGenerator ilEmitter)
        {
            this.Function = function;
            this.ILEmitter = ilEmitter;
        }

        public void EmitInstructions(List<IRInstruction> instructions)
        {
            foreach(IRInstruction instruction in instructions)
            {
                EmitInstruction(instruction);
            }
        }

        public void EmitInstruction(IRInstruction instruction)
        {
            switch (instruction)
            {
                case IRString str: EmitLoadString(str); break;
                case IRCall call: EmitCall(call); break;
                case IRReturn ret: EmitReturn(ret); break;
            }
        }

        void EmitLoadString(IRString str)
        {
            ILEmitter.Emit(OpCodes.Ldstr, str.Value);
        }

        void EmitCall(IRCall call)
        {
            foreach(IRValueInstruction argument in call.Arguments)
            {
                EmitInstruction(argument);
            }

            if(call.Function is IRFunctionImport functionImport)
            {
                ILEmitter.Emit(OpCodes.Call, functionImport.CILMethod);
            }
            else
            {
                ILEmitter.Emit(OpCodes.Call, call.Function.CILMethod);
            }
        }

        void EmitReturn(IRReturn ret)
        {
            ILEmitter.Emit(OpCodes.Ret);
        }
    }
}
