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

        void EmitFunctionBindings()
        {
            foreach(IRFunction function in this.Module.Functions)
            {
                Builder.AppendLine(WasmJSBindingsScripts.CreateJSFunctionExport(function));
            }
        }

        void EmitInit()
        {
            Builder.AppendLine(WasmJSBindingsScripts.GetInitScript($"{this.Module.Name}_module.wasm", GenerateJSImportBindings()));
        }

        string GenerateJSImportBindings()
        {
            StringBuilder builder = new StringBuilder();
            var groupedImports = this.Module.FunctionImports.GroupBy(f => f.Module);

            foreach(var importGroup in groupedImports)
            {
                builder.Append($"   {importGroup.First().Module}: {{\n");

                foreach(IRFunctionImport importedFunction in importGroup)
                {
                    string parameters = string.Join(", ", importedFunction.Parameters.Select(p => p.Name));
                    builder.Append($"       {importedFunction.Name}: ({parameters}) => {importedFunction.Module}.{importedFunction.Name}({parameters}),\n");
                }

                builder.Append("    },\n");
            }

            return builder.ToString();
        }
    }
}
