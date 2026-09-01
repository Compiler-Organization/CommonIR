using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CommonIR.Generators.CIL.Translation
{
    internal class CILMetadataTranslator
    {
        public static ManagedPEBuilder CreateMetadata(IRModule module, PersistedAssemblyBuilder assemblyBuilder)
        {
            MetadataBuilder metadataBuilder = assemblyBuilder.GenerateMetadata(
                out BlobBuilder ilStream,
                out BlobBuilder mappedFieldData
            );

            MethodDefinitionHandle entryPointHandle = (module.EntryPoint != null && module.EntryPoint.CILMethod != null) 
                ? MetadataTokens.MethodDefinitionHandle(module.EntryPoint.CILMethod.MetadataToken)
                : default;

            ManagedPEBuilder peBuilder = new ManagedPEBuilder(
                header: PEHeaderBuilder.CreateExecutableHeader(),
                metadataRootBuilder: new MetadataRootBuilder(metadataBuilder),
                ilStream: ilStream,
                mappedFieldData: mappedFieldData,
                entryPoint: entryPointHandle
            );

            return peBuilder;
        }
    }
}
