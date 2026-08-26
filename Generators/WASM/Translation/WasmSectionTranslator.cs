using CommonIR.Errors;
using CommonIR.Generators.WASM.Emit;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System.Text;

namespace CommonIR.Generators.WASM.Translation
{
    /// <summary>
    /// Translates IR objects into their WebAssembly representation.
    /// </summary>
    internal class WasmSectionTranslator
    {
        IRModule Module { get; set; }

        WasmFactorizedFunctions FactorizedFunctions { get; set; }

        public WasmSectionTranslator(IRModule module, WasmFactorizedFunctions factorizedFunctions)
        {
            this.Module = module;
            this.FactorizedFunctions = factorizedFunctions;
        }

        // NOTE: To anyone reading, each section needs to be in cronological order (except 0x00 custom), as the order of sections in a WASM module is important.
        // You can see the order of the sections in WasmSectionIDs. the data section is translated before the code section to handle forward references.
        public List<WasmSection> TranslateSections()
        {
            List<WasmSection> sections = new List<WasmSection>()
            {
                TranslateTypeSection(),
                TranslateImportSection(),
                TranslateFunctionSection(),
                // TranslateMemorySection(),
                TranslateGlobalSection(),
                TranslateExportSection(),
                TranslateStartSection(),
            };

            WasmDataSection dataSection = TranslateDataSection();
            WasmCodeSection codeSection = TranslateCodeSection();

            sections.Add(codeSection);
            sections.Add(dataSection);

            return sections;
        }

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
                    ModuleName = functionImport.ModuleName,
                    FieldName = functionImport.Name,
                    Kind = WasmImportKind.Function,
                    TypeIndex = (uint)functionImport.Offset
                });
            }

            importSection.Imports.Add(new WasmImport
            {
                ModuleName = "env",
                FieldName = "memory",
                Kind = WasmImportKind.Memory,
                MinLimits = 256,
                MaxLimits = null
            });

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

        private WasmMemorySection TranslateMemorySection()
        {
            WasmMemorySection memorySection = new WasmMemorySection();

            memorySection.Memories.Add(new WasmMemoryLimits
            {
                MinPages = 1,// 64 KiB
                MaxPages = null
            });

            return memorySection;
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
                        .. new WasmInstructionEmitter(this.Module.EntryPoint ?? null, this.FactorizedFunctions).EmitInstruction(global.InitialValue), 
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
                    IsSet = true,
                };

                return startSection;
            }

            return new WasmStartSection();
        }

        private WasmCodeSection TranslateCodeSection()
        {
            WasmCodeSection codeSection = new WasmCodeSection();

            foreach (IRFunction function in Module.Functions)
            {
                WasmInstructionEmitter instructionEmitter = new WasmInstructionEmitter(function, this.FactorizedFunctions);
                List<byte> bodyBytes = instructionEmitter.EmitInstructions(function.Entryblock.Instructions);

                bodyBytes.Add((byte)WasmInstructions.End);

                WasmFunctionBody wasmFunctionBody = new WasmFunctionBody()
                {
                    Instructions = [.. bodyBytes],
                    Locals = TranslateLocals(function.Locals)
                };

                codeSection.Functions.Add(wasmFunctionBody);
            }

            return codeSection;
        }

        private List<WasmLocalGroup> TranslateLocals(List<IRLocal> locals)
        {
            if (locals == null || !locals.Any()) return new List<WasmLocalGroup>();

            int groupIndex = 0;

            return locals
                .Select((local, index) => new
                {
                    Type = WasmTypeTranslator.TranslateIRDataType(local.ValueType.DataType),
                    Index = index
                })
                .Select((item, index) => new
                {
                    item.Type,
                    GroupKey = (index > 0 && item.Type != WasmTypeTranslator.TranslateIRDataType(locals[index - 1].ValueType.DataType))
                        ? ++groupIndex
                        : groupIndex
                })
                .GroupBy(g => new
                {
                    g.GroupKey,
                    g.Type
                })
                .Select(g => new WasmLocalGroup
                {
                    Type = g.Key.Type,
                    Count = (uint)g.Count()
                })
                .ToList();
        }

        private WasmDataSection TranslateDataSection()
        {
            WasmDataSection dataSection = new WasmDataSection();
            ulong currentMemoryAddress = 0;
            var irStrings = this.Module.Objects.OfType<IRString>().ToList();

            foreach (IRString irString in irStrings)
            {
                List<byte> totalSegmentData = [.. Encoding.UTF8.GetBytes(irString.Value)];

                irString.Offset = currentMemoryAddress;

                List<byte> offsetExpression =
                [
                    (byte)WasmInstructions.I32_const,
                    .. LEB128.EncodeSigned((int)currentMemoryAddress),
                    (byte)WasmInstructions.End,
                ];

                WasmDataSegment segment = new WasmDataSegment
                {
                    Mode = WasmDataSegmentMode.ActiveImplicitMemory,
                    OffsetExpression = offsetExpression,
                    Data = totalSegmentData
                };

                dataSection.Segments.Add(segment);

                currentMemoryAddress += (ulong)totalSegmentData.Count;
            }

            return dataSection;
        }
    }
}
