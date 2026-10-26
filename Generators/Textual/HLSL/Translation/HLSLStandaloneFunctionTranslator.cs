using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Emission;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    internal class HLSLStandaloneFunctionTranslator
    {
        CommonIRHLSLConfiguration Configuration { get; set; }

        public HLSLStandaloneFunctionTranslator(CommonIRHLSLConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public string TranslateFunction(IRFunction function)
        {
            if (!function.IsKernel)
            {
                throw ErrorHandler.CreateNotImplimented($"HLSL target with standalone function translation only supports kernels, and '{function.Name}' is not a kernel.");
            }

            return HLSLKernelTranslator.Translate(function, this.Configuration);
        }
    }
}
