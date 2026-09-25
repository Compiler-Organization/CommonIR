using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    internal class HLSLTranslator
    {
        public List<SourceFile> TranslateIRModule(IRModule module, CommonIRHLSLConfiguration config)
        {
            List<SourceFile> sourceFiles = new List<SourceFile>();

            switch(config.CompilationType)
            {
                case HLSLCompilationType.StandaloneFunctions:
                    {

                        break;
                    }

                default:
                    throw ErrorHandler.CreateNotImplimented($"Compilation type '{config.CompilationType}' is not supported when translating to HLSL");
            }

            return sourceFiles;
        }

        public List<SourceFile> TranslateIRModuleToStandalone(IRModule module, CommonIRHLSLConfiguration configuration)
        {
            List<SourceFile> sourceFiles = new List<SourceFile>();
            HLSLStandaloneFunctionTranslator standaloneFunctionTranslator = new HLSLStandaloneFunctionTranslator(configuration);

            foreach(IRFunction function in module.Functions)
            {
                string translatedFunction = standaloneFunctionTranslator.TranslateFunction(function);
                sourceFiles.Add(new SourceFile(function.Name, ".hlsl", Encoding.UTF8.GetBytes(translatedFunction)));
            }

            return sourceFiles;
        }
    }
}
