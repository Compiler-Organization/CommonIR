using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Bindings
{
    internal class WasmJSBindingsScripts
    {
        public static string GetInitScript(IRModule module, string wasmFileName, string importBindings) => @"async function initWasmModule(wasmUrl, importObject = {}) {
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

const MAX_SAFARI_DECODE_BYTES = 16 * 1024;
let numBytesDecoded = 0;
let cachedTextDecoder = new TextDecoder('utf-8', { ignoreBOM: true, fatal: true });

let cachedUint8ArrayMemory = null;
export function getMemoryAsUint8Array() {
    if(cachedUint8ArrayMemory === null || cachedUint8ArrayMemory.buffer !== wasmImports.env.memory.buffer) {
        cachedUint8ArrayMemory = new Uint8Array(wasmImports.env.memory.buffer);
    }
    return cachedUint8ArrayMemory;
}

function decodeText(ptr, len) {
    numBytesDecoded += len;
    if (numBytesDecoded >= MAX_SAFARI_DECODE_BYTES) {
        cachedTextDecoder = new TextDecoder('utf-8', { ignoreBOM: true, fatal: true });
        cachedTextDecoder.decode();
        numBytesDecoded = len;
    }

    return cachedTextDecoder.decode(getMemoryAsUint8Array().subarray(ptr, ptr + len));
}

function getStringFromWasm(ptr, len) {
    const unsignedPtr = ptr >>> 0;

    return decodeText(unsignedPtr, len);
}

let wasm;

const wasmImports = {
" + importBindings + @"
    env: {
        memory: new WebAssembly.Memory({ initial: 256 }),
        on_wasm_error: (errorCode) => console.error(`Wasm error occurred: ${errorCode}`),
        print_int: (value) => console.log(`Output from Wasm: ${value}`)
    }
};

async function init() {
    const wasmInstance = await initWasmModule('" + $"./{wasmFileName}" + @"', wasmImports);
    wasm = wasmInstance.exports;
  
    if (!(wasmInstance && wasmInstance.exports)) {
        console.error('Failed to initialize webassembly.');
    }" + (module.EntryPoint == null ? "" : $"\n\n    wasm.{module.EntryPoint.Name}();") + @"
}

export { init as default }
";
    }
}
