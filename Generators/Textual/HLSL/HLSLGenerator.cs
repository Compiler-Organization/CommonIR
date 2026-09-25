using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL
{
    public class HLSLGenerator
    {
        IRModule Module { get; set; }

        CommonIRHLSLConfiguration Configuration { get; set; }

        public HLSLGenerator(IRModule module, CommonIRHLSLConfiguration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            HLSLTranslator translator = new HLSLTranslator();

            switch(this.Configuration.CompilationType)
            {
                case HLSLCompilationType.StandaloneFunctions:
                    return translator.TranslateIRModuleToStandalone(this.Module, this.Configuration);

                default:
                    throw ErrorHandler.CreateNotImplimented($"Compilation type '{this.Configuration.CompilationType}' is not supported when targetting HLSL.");
            }
        }
    }
}
