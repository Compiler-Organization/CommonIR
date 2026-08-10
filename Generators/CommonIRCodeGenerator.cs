using CommonIR.Errors;
using CommonIR.Generators.WASM;
using CommonIR.IR.Grammar.Objects;
using CommonIR.Passes.Optimization;
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
            IROptimizer optimizer = new IROptimizer(module);
            optimizer.Optimize(this.Settings.OptimizingMode);

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
