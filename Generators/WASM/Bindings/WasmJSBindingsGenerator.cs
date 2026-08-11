using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System.Text;

namespace CommonIR.Generators.WASM.Bindings
{
    internal class WasmJSBindingsGenerator
    {
        StringBuilder Builder { get; set; } = new StringBuilder();

        IRModule Module { get; set; }

        public WasmJSBindingsGenerator(IRModule module)
        {
            this.Module = module;
        }

        public string CreateBindings()
        {
            EmitInit();
            EmitFunctionBindings();
            return Builder.ToString();
        }

        void EmitInit()
        {
            Builder.AppendLine(WasmJSBindingsScripts.GetInitScript($"{this.Module.Name}_module.wasm", GenerateJSImportBindings()));
        }

        void EmitFunctionBindings()
        {
            foreach (IRFunction function in this.Module.Functions)
            {
                Builder.AppendLine(CreateJSFunctionExport(function));
            }
        }

        string CreateJSFunctionExport(IRFunction function)
        {
            string parameters = string.Join(", ", function.Parameters.Select(p => p.Name));
            string arguments = string.Join(", ", function.Parameters.Select(p => WrapWithHelperWriter(p.Name, p.ValueType)));

            return $@"export function {function.Name}({parameters}) {{
    return wasm.{function.Name}({arguments});
}}";
        }

        string GenerateJSImportBindings()
        {
            StringBuilder builder = new StringBuilder();
            var groupedImports = this.Module.FunctionImports.GroupBy(f => f.ModuleName);

            foreach(var importGroup in groupedImports)
            {
                builder.Append($"   {importGroup.First().ModuleName}: {{\n");

                foreach(IRFunctionImport importedFunction in importGroup)
                {
                    string parameters = string.Join(", ", importedFunction.Parameters.Select(p => p.Name));
                    string arguments = string.Join(", ", importedFunction.Parameters.Select(p => WrapWithHelperReader(p.Name, p.ValueType)));
                    builder.Append($"       {importedFunction.Name}: ({parameters}) => {importedFunction.ModuleName}.{importedFunction.Name}({arguments}),\n");
                }

                builder.Append("    },\n");
            }

            return builder.ToString();
        }

        string WrapWithHelperReader(string value, IRType type)
        {
            return type.DataType switch
            {
                IRDataTypes.String => $"getStringFromWasm({value})",
                _ => value
            };
        }

        string WrapWithHelperWriter(string value, IRType type) // TODO: with malloc and all of that
        {
            return type.DataType switch
            {
                _ => value
            };
        }
    }
}
