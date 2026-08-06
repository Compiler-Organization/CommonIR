using CommonIR.Errors;
using CommonIR.Generators.WASM.Emit;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    /// <summary>
    /// Translates IR objects into their WebAssembly representation.
    /// </summary>
    internal class WasmMetadataTranslator
    {
        IRModule Module { get; set; }

        public WasmMetadataTranslator(IRModule module)
        {
            this.Module = module;
        }

        // NOTE: To anyone reading, each section needs to be in cronological order (except 0x00 custom), as the order of sections in a WASM module is important.
        // You can see the order of the sections in WasmSectionIDs.
        public List<WasmSection> TranslateMetadataSections()
            => new List<WasmSection>()
            {
                TranslateTypeSection(),
                TranslateImportSection(),
                TranslateFunctionSection(),
                TranslateGlobalSection(),
                TranslateExportSection(),
                TranslateStartSection(),
            };

        private WasmTypeSection TranslateTypeSection()
        {
            WasmTypeSection typeSection = new WasmTypeSection();
            foreach (IRFunctionImport functionImport in this.Module.FunctionImports)
            {
                typeSection.Types.Add(new WasmTypeSectionType
                {
                    Form = WasmForms.Function,
                    ParameterTypes = [.. functionImport.Parameters.Select(p => WasmTypeTranslator.TranslateIRType(p.ValueType))],
                    Returns = [.. functionImport.ReturnTypes.Where(t => t.DataType != IRDataTypes.Void).Select(WasmTypeTranslator.TranslateIRType)]
                });
                functionImport.Offset = (ulong)(typeSection.Types.Count - 1);
            }

            foreach (IRFunction function in this.Module.Functions)
            {
                typeSection.Types.Add(new WasmTypeSectionType
                {
                    Form = WasmForms.Function,
                    ParameterTypes = [.. function.Parameters.Select(p => WasmTypeTranslator.TranslateIRType(p.ValueType))],
                    Returns = [.. function.ReturnTypes.Where(t => t.DataType != IRDataTypes.Void).Select(WasmTypeTranslator.TranslateIRType)]
                });
                function.Offset = (ulong)(typeSection.Types.Count - 1);
            }
            return typeSection;
        }

        private WasmImportSection TranslateImportSection()
        {
            WasmImportSection importSection = new WasmImportSection();
            foreach (IRFunctionImport functionImport in this.Module.FunctionImports)
            {
                importSection.Imports.Add(new WasmImport
                {
                    ModuleName = functionImport.Module,
                    FieldName = functionImport.Name,
                    Kind = WasmImportKind.Function,
                    TypeIndex = (uint)functionImport.Offset
                });
            }
            return importSection;
        }

        private WasmFunctionSection TranslateFunctionSection()
        {
            WasmFunctionSection functionSection = new WasmFunctionSection();
            foreach (IRFunction function in this.Module.Functions)
            {
                functionSection.TypeIndices.Add(function.Offset);
            }
            return functionSection;
        }

        private WasmGlobalSection TranslateGlobalSection()
        {
            WasmGlobalSection globalSection = new WasmGlobalSection();

            foreach(IRGlobal global in this.Module.Globals)
            {
                WasmGlobalEntry globalEntry = new WasmGlobalEntry
                {
                    IsMutable = global.IsMutable,
                    Type = WasmTypeTranslator.TranslateIRType(global.ValueType),
                    InitializationExpression = [
                        .. new WasmInstructionEmitter().EmitInstruction(global.InitialValue), 
                        (byte)WasmInstructions.End
                    ]
                };

                globalSection.Globals.Add(globalEntry);
            }

            return globalSection;
        }

        private WasmExportSection TranslateExportSection()
        {
            WasmExportSection exportSection = new WasmExportSection();
            foreach (IRFunction function in this.Module.Functions)
            {
                exportSection.Exports.Add(new WasmExportEntry
                {
                    Name = function.Name,
                    Kind = WasmExportKind.Function,
                    Index = function.Offset
                });
            }
            return exportSection;
        }

        private WasmStartSection TranslateStartSection() // TODO: Move this to the bindings generator - bad practice using the start section.
                                                         // Could use the start sections for entrypoints with 0 returns and 0 parameters,
                                                         // if that saves a couple cycles...
        {
            if (this.Module.EntryPoint != null)
            {
                if (this.Module.EntryPoint.HasReturn() || this.Module.EntryPoint.HasParameters())
                {
                    throw ErrorHandler.Create($"Entry point \"{this.Module.EntryPoint.Name}\" signature must have exactly 0 returns and 0 parameters!");
                }

                WasmStartSection startSection = new WasmStartSection()
                {
                    StartFunctionIndex = this.Module.EntryPoint.Offset,
                };

                return startSection;
            }

            return new WasmStartSection();
        }
    }
}
