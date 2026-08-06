using CommonIR.Errors;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    public class WasmTranslator
    {
        public WasmModule TranslateIRModule(IRModule iRModule)
        {
            WasmModule wasmModule = new WasmModule();

            WasmMetadataTranslator objectTranslator = new WasmMetadataTranslator(iRModule);
            wasmModule.Sections.AddRange(objectTranslator.TranslateMetadataSections());

            

            WasmFunctionTranslator instructionTranslator = new WasmFunctionTranslator(iRModule);
            wasmModule.Sections.Add(instructionTranslator.TranslateFunctionBodies());

            return wasmModule;
        }
    }
}
