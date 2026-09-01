using CommonIR.Errors;
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
            Builder.AppendLine(WasmJSBindingsScripts.GetInitScript(this.Module, $"{this.Module.Name}_module.wasm", GenerateJSImportBindings()));
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
            string parameters = ConvertParameters(function.Parameters);
            string arguments = WrapWithHelperReaders(function.Parameters);

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
                    string parameters = ConvertParameters(importedFunction.Parameters);
                    string arguments = WrapWithHelperReaders(importedFunction.Parameters);
                    builder.Append($"       {importedFunction.Name}: ({parameters}) => {importedFunction.ModuleName}.{importedFunction.Name}({arguments}),\n");
                }

                builder.Append("    },\n");
            }

            return builder.ToString();
        }

        string ConvertParameters(List<IRLocal> parameters)
        {
            List<string> builder = new List<string>();

            foreach(IRLocal parameter in parameters)
            {
                if(parameter.ValueType.IsFatPointer)
                {
                    builder.Add(parameter.Name);
                    builder.Add(parameter.LengthCompanion.Name);
                }
                else
                {
                    builder.Add(parameter.Name);
                }
            }

            return string.Join(", ", builder);
        }

        string WrapWithHelperReaders(List<IRLocal> locals)
        {
            List<string> convertedLocals = new List<string>();

            for(int i = 0; i < locals.Count; i++)
            {
                IRLocal local = locals[i];
                if(local.ValueType.IsFatPointer)
                {
                    if(local.LengthCompanion == null)
                    {
                        throw ErrorHandler.Create($"Length companion to fat pointer '{local.Name}' was never declared.");
                    }

                    convertedLocals.Add(WrapWithHelperReader($"{local.LengthCompanion.Name}, {local.Name}", local.ValueType));
                    
                    if(i + 1 < locals.Count && locals[i + 1] == local.LengthCompanion)
                    {
                        i++;
                    }
                }
                else 
                {
                    convertedLocals.Add(WrapWithHelperReader(local.Name, local.ValueType));
                }
            }

            return string.Join(", ", convertedLocals);
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
