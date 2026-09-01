using CommonIR.Errors;
using CommonIR.Generators.CIL.Metadata;
using CommonIR.Generators.CIL.Translation;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace CommonIR.Generators.CIL
{
    public class CILGenerator
    {
        IRModule Module { get; set; }
        CommonIRCILConfiguration CILConfiguration { get; set; }

        public CILGenerator(IRModule module, CommonIRCILConfiguration cilConfiguration)
        {
            this.Module = module;
            this.CILConfiguration = cilConfiguration;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            ManagedPEBuilder peBuilder = CILTranslator.TranslateIRModule(this.Module);

            BlobBuilder peBlob = new BlobBuilder();
            peBuilder.Serialize(peBlob);

            return [
                new SourceFile(this.Module.Name, ".dll", peBlob.ToArray()),
                new SourceFile($"{this.Module.Name}.runtimeconfig", ".json", Encoding.UTF8.GetBytes(CILRuntimeconfigGenerator.CreateConfig(this.CILConfiguration)))
                ];
        }
    }
}
