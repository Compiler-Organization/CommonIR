using CommonIR.Generators.Binary.X8664.Translation;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664
{
    public class X8664Generator
    {
        IRModule Module { get; set; }

        CommonIRX8664Configuration Configuration { get; set; }

        public X8664Generator(IRModule module, CommonIRX8664Configuration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            X8664Translator translator = new X8664Translator(this.Module, this.Configuration);
            byte[] executable = translator.Translate();

            return [new SourceFile(this.Module.Name, ".exe", executable), .. translator.ShaderSources];
        }
    }
}
