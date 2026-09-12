using CommonIR.Generators.Binary.WASM.Model;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.Binary.WASM.Emission
{
    internal class WasmModuleEmitter
    {
        public WasmModule Emit(IRModule module)
        {
            return new WasmModule
            {
                Sections = new List<WasmSection>()
            };
        }
    }
}
