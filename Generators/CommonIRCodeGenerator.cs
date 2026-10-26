using CommonIR.Errors;
using CommonIR.Generators.Binary.CIL;
using CommonIR.Generators.Binary.WASM;
using CommonIR.Generators.Binary.X8664;
using CommonIR.Generators.Textual.HLSL;
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
                case CommonIRTargets.WebAssembly:
                    {
                        WasmGenerator wasmGenerator = new WasmGenerator(module);
                        return wasmGenerator.GenerateSourceFiles();
                    }

                case CommonIRTargets.CommonIntermediateLanguage:
                    {
                        CILGenerator cilGenerator = new CILGenerator(module, Settings.TargetConfiguration as CommonIRCILConfiguration ?? throw ErrorHandler.Create("Could not create target configuration for Common Intermediate Language."));
                        return cilGenerator.GenerateSourceFiles();
                    }

                case CommonIRTargets.HLSL:
                    {
                        HLSLGenerator hlslGenerator = new HLSLGenerator(module, Settings.TargetConfiguration as CommonIRHLSLConfiguration ?? throw ErrorHandler.Create("Could not create target configuration for HLSL."));
                        return hlslGenerator.GenerateSourceFiles();
                    }

                case CommonIRTargets.X8664:
                    {
                        X8664Generator x8664Generator = new X8664Generator(module, Settings.TargetConfiguration as CommonIRX8664Configuration ?? throw ErrorHandler.Create("Could not create target configuration for x86-64."));
                        return x8664Generator.GenerateSourceFiles();
                    }

                default:
                    throw ErrorHandler.Create($"Target {Settings.Target} is not supported.");
            }
        }
    }
}
