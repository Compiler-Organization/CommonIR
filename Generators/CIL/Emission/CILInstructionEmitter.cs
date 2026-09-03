using CommonIR.Errors;
using CommonIR.Generators.CIL.Translation;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.Generators.CIL.Emission
{
    internal class CILInstructionEmitter
    {
        ILGenerator ILEmitter { get; set; }

        CILScratchPool ScratchPool { get; set; }

        IRFunction Function { get; set; }

        public CILInstructionEmitter(IRFunction function, ILGenerator ilEmitter)
        {
            this.Function = function;
            this.ILEmitter = ilEmitter;
            this.ScratchPool = new CILScratchPool(function);
        }

        public void EmitInstructions(List<IRInstruction> instructions)
        {
            foreach(IRInstruction instruction in instructions)
            {
                EmitInstruction(instruction);
            }
        }

        public void EmitValueInstructions(List<IRValueInstruction> instructions)
        {
            foreach (IRValueInstruction instruction in instructions)
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
                case IRStruct _struct: EmitInitializeStruct(_struct); break;
                default:
                    throw ErrorHandler.Create($"No CIL translation implemented for instruction '{instruction.GetType().Name}'");
            }
        }

        void EmitInitializeStruct(IRStruct _struct)
        {
            Type structType = CILTypeTranslator.TranslateIRType(_struct.ValueType);
            LocalBuilder temp = ScratchPool.Borrow(structType);

            EmitLdloca(temp);
            ILEmitter.Emit(OpCodes.Initobj, structType);

            EmitLdloc(temp);

            ScratchPool.Return(temp);
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
                ILEmitter.Emit(OpCodes.Call, functionImport.CILMethod!);
            }
            else
            {
                ILEmitter.Emit(OpCodes.Call, call.Function.CILMethod!);
            }
        }

        void EmitReturn(IRReturn ret)
        {
            if(ret.Values != null && ret.Values.Count > 0)
            {
                EmitValueInstructions(ret.Values);
            }

            if(ret.Values != null && ret.Values.Count > 1)
            {
                ConstructorInfo? valueTupleConstructor = CILTypeTranslator.CreateValueTupleTypeConstructor(ret.Values.Select(v => CILTypeTranslator.TranslateIRType(v.ValueType)).ToArray());

                if(valueTupleConstructor == null)
                {
                    throw ErrorHandler.Create($"Could not find ValueTuple constructor for {ret.Values.Count} values");
                }

                ILEmitter.Emit(OpCodes.Newobj, valueTupleConstructor);
            }

            ILEmitter.Emit(OpCodes.Ret);
        }

        void EmitStloc(LocalBuilder localBuilder)
        {
            switch(localBuilder.LocalIndex)
            {
                case 0: ILEmitter.Emit(OpCodes.Stloc_0); return;
                case 1: ILEmitter.Emit(OpCodes.Stloc_1); return;
                case 2: ILEmitter.Emit(OpCodes.Stloc_2); return;
                case 3: ILEmitter.Emit(OpCodes.Stloc_3); return;
            }

            if(localBuilder.LocalIndex <= byte.MaxValue)
            {
                ILEmitter.Emit(OpCodes.Stloc_S, localBuilder);
                return;
            }

            ILEmitter.Emit(OpCodes.Stloc, localBuilder);
        }

        void EmitLdloc(LocalBuilder localBuilder)
        {
            switch (localBuilder.LocalIndex)
            {
                case 0: ILEmitter.Emit(OpCodes.Ldloc_0); return;
                case 1: ILEmitter.Emit(OpCodes.Ldloc_1); return;
                case 2: ILEmitter.Emit(OpCodes.Ldloc_2); return;
                case 3: ILEmitter.Emit(OpCodes.Ldloc_3); return;
            }

            if (localBuilder.LocalIndex <= byte.MaxValue)
            {
                ILEmitter.Emit(OpCodes.Ldloc_S, localBuilder);
                return;
            }

            ILEmitter.Emit(OpCodes.Ldloc, localBuilder);
        }

        void EmitLdloca(LocalBuilder localBuilder)
        {
            if (localBuilder.LocalIndex <= byte.MaxValue)
            {
                ILEmitter.Emit(OpCodes.Ldloca_S, localBuilder);
                return;
            }
            ILEmitter.Emit(OpCodes.Ldloca, localBuilder);
        }
    }
}
