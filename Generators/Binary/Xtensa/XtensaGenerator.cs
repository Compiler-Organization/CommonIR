using CommonIR.Generators.Binary.Xtensa.Translation;
using CommonIR.IR.Grammar.Objects;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.Xtensa
{
    public class XtensaGenerator
    {
        IRModule Module { get; }

        CommonIRXtensaConfiguration Configuration { get; }

        public XtensaGenerator(IRModule module, CommonIRXtensaConfiguration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            XtensaTranslator translator = new XtensaTranslator(this.Module, this.Configuration);
            byte[] image = translator.Translate();
            string summary = $"code {translator.CodeSize}\ndata {translator.DataSize}\nheap {translator.HeapSize}\n";

            return [new SourceFile(this.Module.Name, ".bin", image), new SourceFile(this.Module.Name, ".map", Encoding.UTF8.GetBytes(summary))];
        }
    }
}
