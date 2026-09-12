using CommonIR.Errors;
using CommonIR.Generators.Binary.WASM.Emission;
using CommonIR.Generators.Binary.WASM.Model;
using CommonIR.Generators.Binary.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.Binary.WASM.Translation
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
