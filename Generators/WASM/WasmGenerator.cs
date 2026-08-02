using CommonIR.Generators.WASM.Bindings;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Translation;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM
{
    public class WasmGenerator
    {
        IRModule Module { get; set; }

        public WasmGenerator(IRModule module)
        {
            this.Module = module;
        }

        public List<SourceFile> GenerateSourceFiles()
        {
            WasmTranslator wasmTranslator = new WasmTranslator();
            WasmModule wasmModule = wasmTranslator.TranslateIRModule(Module);

            WasmJSBindingsGenerator bindingsGenerator = new WasmJSBindingsGenerator(Module);

            return [
                new SourceFile("wasm_module", ".wasm", wasmModule.Serialize()),
                new SourceFile("wasm_bindings", ".js", Encoding.UTF8.GetBytes(bindingsGenerator.CreateBindings())),
                ];
        }
    }
}
