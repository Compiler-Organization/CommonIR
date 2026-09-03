using CommonIR.Generators.CIL.Emission;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CommonIR.Generators.CIL.Translation
{
    internal class CILTranslator
    {
        public static ManagedPEBuilder TranslateIRModule(IRModule module)
        {
            AssemblyName assemblyName = new AssemblyName(module.Name) 
            {
                Version = new Version(1, 0, 0, 0)
            };

            PersistedAssemblyBuilder assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, Type.GetType("System.Object, System.Runtime")!.Assembly);
            module.CILModule = assemblyBuilder.DefineDynamicModule(module.Name);
            module.CILType = module.CILModule.DefineType(
                "Program", 
                TypeAttributes.Public | TypeAttributes.Class
            );

            CILTypeTranslator.Module = module;

            CILFunctionTranslator.CreateFunctionReferences(module, module.CILType);
            CILFunctionTranslator.CreateFunctionBodies(module);

            module.CILType.CreateType();

            ManagedPEBuilder peBuilder = CILMetadataTranslator.CreateMetadata(module, assemblyBuilder);

            return peBuilder;
        }
    }
}
