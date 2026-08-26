using CommonIR.Errors;
using CommonIR.Generators.WASM.Emit;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    public class WasmTranslator
    {
        public WasmModule TranslateIRModule(IRModule module)
        {
            WasmModule wasmModule = new WasmModule();

            WasmFactorizedFunctions factorizedFunctions = new WasmFactorizedFunctions(module);
            WasmSectionTranslator objectTranslator = new WasmSectionTranslator(module, factorizedFunctions);
            wasmModule.Sections.AddRange(objectTranslator.TranslateSections());

            return wasmModule;
        }
    }
}
