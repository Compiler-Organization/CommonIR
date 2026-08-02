using CommonIR.IR.Grammar.Objects;
using System.Text;

namespace CommonIR.Generators.WASM.Bindings
{
    internal class WasmJSBindingsGenerator
    {
        StringBuilder Builder { get; set; } = new StringBuilder();

        public WasmJSBindingsGenerator(IRModule iRModule)
        {
        }

        public string CreateBindings()
        {
            return Builder.ToString();
        }

        void EmitFunctionBindings(IRFunctionImport functionImport)
        {
            Builder.AppendLine($"export function {functionImport.Name}() {{");
            Builder.AppendLine($"    return wasmInstance.exports.{functionImport.Name}();");
            Builder.AppendLine($"}}");
        }
    }
}
