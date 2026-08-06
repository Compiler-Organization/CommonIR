using CommonIR.Errors;
using CommonIR.Generators.WASM;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators
{
    public class CommonIRCodeGenerator
    {
        CommonIRCodeGeneratorSettings Settings { get; set; }

        public CommonIRCodeGenerator(CommonIRCodeGeneratorSettings settings)
        {
            this.Settings = settings;
        }

        public List<SourceFile> GenerateSourceFiles(IRModule module)
        {
            switch(Settings.Target)
            {
                case CommonIRTargets.WebAssembly_1_0_MVP:
                    {
                        WasmGenerator wasmGenerator = new WasmGenerator(module);
                        return wasmGenerator.GenerateSourceFiles();
                    }

                default:
                    throw ErrorHandler.Create($"Target {Settings.Target} is not supported.");
            }
        }
    }
}
