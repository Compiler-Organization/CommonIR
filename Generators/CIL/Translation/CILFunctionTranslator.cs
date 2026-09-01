using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Reflection;
using CommonIR.Errors;
using CommonIR.Generators.CIL.Emit;

namespace CommonIR.Generators.CIL.Translation
{
    internal class CILFunctionTranslator
    {
        public static void CreateFunctionReferences(IRModule module, TypeBuilder typeBuilder)
        {
            foreach(IRFunction function in module.Functions)
            {
                function.CILMethod = typeBuilder.DefineMethod(
                    function.Name,
                    MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static,
                    CILTypeTranslator.TranslateIRTypes(function.ReturnTypes),
                    function.Parameters.Select(p => CILTypeTranslator.TranslateIRType(p.ValueType)).ToArray()
                );
            }

            foreach(IRFunctionImport functionImport in module.FunctionImports)
            {
                Type? type = Type.GetType($"{functionImport.ModuleName}, {functionImport.ModuleName}");
                if(type == null)
                {
                    throw ErrorHandler.Create($"Function import type '{functionImport.ModuleName}, {functionImport.ModuleName}' does not exist in the current context.");
                }

                MethodInfo? methodInfo = type.GetMethod(functionImport.Name, functionImport.Parameters.Select(p => CILTypeTranslator.TranslateIRType(p.ValueType)).ToArray());
                if(methodInfo == null)
                {
                    throw ErrorHandler.Create($"Function import method '{functionImport.Name}' does not exist in type '{functionImport.ModuleName}, {functionImport.ModuleName}'");
                }

                functionImport.CILMethod = methodInfo;
            }
        }

        public static void CreateFunctionBodies(IRModule module)
        {
            foreach(IRFunction function in module.Functions)
            {
                ILGenerator ilEmitter = function.CILMethod!.GetILGenerator();
                CILInstructionEmitter instructionEmitter = new CILInstructionEmitter(function, ilEmitter);
                instructionEmitter.EmitInstructions(function.Entryblock.Instructions);
            }
        }
    }
}
