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

            WasmMemoryFunctionEmitter memoryFunctionEmitter = new WasmMemoryFunctionEmitter(module);
            IRFunction malloc = memoryFunctionEmitter.EmitMalloc();
            IRFunction free = memoryFunctionEmitter.EmitFree();

            WasmSectionTranslator objectTranslator = new WasmSectionTranslator(module, malloc, free);
            wasmModule.Sections.AddRange(objectTranslator.TranslateSections());

            return wasmModule;
        }
    }
}
