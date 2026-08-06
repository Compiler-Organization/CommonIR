using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Bindings
{
    internal class WasmJSBindingsScripts
    {
        public static string GetInitScript(string wasmFileName, string importBindings) => @"async function initWasmModule(wasmUrl, importObject = {}) {
    try {
        if (typeof WebAssembly.instantiateStreaming === 'function') {
            const response = fetch(wasmUrl);
            const { instance } = await WebAssembly.instantiateStreaming(response, importObject);
            return instance;
        }
    
        console.warn('instantiateStreaming not supported. Falling back to arrayBuffer.');
        const response = await fetch(wasmUrl);
        const bytes = await response.arrayBuffer();
        const { instance } = await WebAssembly.instantiate(bytes, importObject);
        return instance;
    
    } catch (error) {
        console.error(`Failed to initialize Wasm module from ${wasmUrl}:`, error);
        throw error;
    }
}

let wasm;

const wasmImports = {
" + importBindings + @"
    env: {
        memory: new WebAssembly.Memory({ initial: 256, maximum: 512 }),
        on_wasm_error: (errorCode) => console.error(`Wasm error occurred: ${errorCode}`),
        print_int: (value) => console.log(`Output from Wasm: ${value}`)
    }
};

async function init() {
    const wasmInstance = await initWasmModule('" + $"./{wasmFileName}" + @"', wasmImports);
    wasm = wasmInstance.exports;
  
    if (!(wasmInstance && wasmInstance.exports)) {
        console.error('Failed to initialize webassembly.');
    }
}

export { init as default }
";

        public static string CreateJSFunctionExport(IRFunction function)
        {
            string parameters = string.Join(", ", function.Parameters.Select(p => p.Name));

            return $@"export function {function.Name}({parameters}) {{
    return wasm.{function.Name}({parameters});
}}";
        }
    }
}
