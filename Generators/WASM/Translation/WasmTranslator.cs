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
            IRGlobal heapPointer = memoryFunctionEmitter.EmitHeapPointer();
            IRFunction malloc = memoryFunctionEmitter.EmitMalloc(heapPointer);
            IRFunction free = memoryFunctionEmitter.EmitFree(heapPointer);

            WasmSectionTranslator objectTranslator = new WasmSectionTranslator(module, malloc, free);
            wasmModule.Sections.AddRange(objectTranslator.TranslateSections());

            return wasmModule;
        }
    }
}
