using CommonIR.Errors;
using CommonIR.Generators.Binary.WASM.Emission;
using CommonIR.Generators.Binary.WASM.Model;
using CommonIR.Generators.Binary.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System.Text;

namespace CommonIR.Generators.Binary.WASM.Translation
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
            WasmTypeSection typeSection = TranslateTypeSection();

            List<WasmSection> sections = new List<WasmSection>()
            {
                typeSection,
                TranslateImportSection(),
                TranslateFunctionSection(),
                TranslateTableSection(),
                // TranslateMemorySection(),
                TranslateGlobalSection(),
                TranslateExportSection(),
                TranslateStartSection(),
                TranslateElementSection(),
            };

            this.FactorizedFunctions.TypeIndexOf = (parameters, results) => TypeIndexOf(typeSection, parameters, results);

            WasmDataSection dataSection = TranslateDataSection();
            WasmCodeSection codeSection = TranslateCodeSection();

            sections.Add(codeSection);
            sections.Add(dataSection);

            return sections;
        }

        List<IRType> FlattenReturnTypes(List<IRType> returnTypes)
        {
            List<IRType> flattenedReturnTypes = new List<IRType>();

            foreach (IRType retType in returnTypes)
            {
                if (retType.IsFatPointer)
                {
                    flattenedReturnTypes.Add(new IRType(IRDataTypes.Int32));
                    flattenedReturnTypes.Add(new IRType(IRDataTypes.Int32));
                }
                else
                {
                    flattenedReturnTypes.Add(retType);
                }
            }
            return flattenedReturnTypes;
        }

        List<IRLocal> FlattenParameters(List<IRLocal> parameters)
        {
            List<IRLocal> flattenedParameters = new List<IRLocal>();

            foreach (IRLocal parameter in parameters)
            {
                if (parameter.ValueType.IsFatPointer)
                {
                    IRLocal aggregatePointer = new IRLocal($"{parameter.Name}_ptr", parameter.ValueType, parameter.IsMutable)
                    {
                        Offset = (ulong)flattenedParameters.Count
                    };
                    flattenedParameters.Add(aggregatePointer);

                    IRLocal lengthCompanion = new IRLocal($"{parameter.Name}_len", new IRType(IRDataTypes.Int32), parameter.IsMutable)
                    {
                        Offset = (ulong)flattenedParameters.Count
                    };
                    flattenedParameters.Add(lengthCompanion);

                    aggregatePointer.LengthCompanion = lengthCompanion;
                }
                else
                {
                    parameter.Offset = (ulong)flattenedParameters.Count;
                    flattenedParameters.Add(parameter);
                }
            }

            return flattenedParameters;
        }

        private WasmTypeSection TranslateTypeSection()
        {
            WasmTypeSection typeSection = new WasmTypeSection();
            foreach (IRFunctionImport functionImport in this.Module.FunctionImports)
            {
                List<IRType> returnTypes = FlattenReturnTypes(functionImport.ReturnTypes);
                List<IRLocal> parameters = FlattenParameters(functionImport.Parameters);

                typeSection.Types.Add(new WasmTypeSectionType
                {
                    Form = WasmForms.Function,
                    ParameterTypes = [.. parameters.Select(p => WasmTypeTranslator.TranslateIRType(p.ValueType))],
                    Returns = [.. returnTypes.Where(t => t.DataType != IRDataTypes.Void).Select(WasmTypeTranslator.TranslateIRType)]
                });
                functionImport.Offset = (ulong)(typeSection.Types.Count - 1);
            }

            foreach (IRFunction function in this.Module.Functions)
            {
                List<IRType> returnTypes = FlattenReturnTypes(function.ReturnTypes);
                List<IRLocal> parameters = FlattenParameters(function.Parameters);

                typeSection.Types.Add(new WasmTypeSectionType
                {
                    Form = WasmForms.Function,
                    ParameterTypes = [.. parameters.Select(p => WasmTypeTranslator.TranslateIRType(p.ValueType))],
                    Returns = [.. returnTypes.Where(t => t.DataType != IRDataTypes.Void).Select(WasmTypeTranslator.TranslateIRType)]
                });
                function.Offset = (ulong)(typeSection.Types.Count - 1);
            }
            return typeSection;
        }

        /// <summary>
        /// Finds the type with exactly these parameters and results, or adds it. Types are only ever appended, so the
        /// indices of the types already used by imports and functions do not change.
        /// </summary>
        static ulong TypeIndexOf(WasmTypeSection typeSection, List<WasmFormTypes> parameters, List<WasmFormTypes> results)
        {
            int index = typeSection.Types.FindIndex(t => t.Form == WasmForms.Function && t.ParameterTypes.SequenceEqual(parameters) && t.Returns.SequenceEqual(results));

            if (index >= 0)
            {
                return (ulong)index;
            }

            typeSection.Types.Add(new WasmTypeSectionType { Form = WasmForms.Function, ParameterTypes = [.. parameters], Returns = [.. results] });
            return (ulong)(typeSection.Types.Count - 1);
        }

        /// <summary>
        /// Every function goes into one table, so a function reference is a table index that call_indirect, and JavaScript
        /// through the exported '__table', can call. Index 0 stays empty, so a null function reference is 0.
        /// </summary>
        private WasmTableSection TranslateTableSection()
        {
            Dictionary<IRFunction, int> tableIndices = this.Module.Functions.Select((function, index) => (function, index)).ToDictionary(f => f.function, f => f.index + 1);

            this.FactorizedFunctions.TableIndexOf = function => tableIndices.TryGetValue(function, out int index)
                ? index
                : throw ErrorHandler.Create($"Function '{function.Name}' is not part of the module.");

            WasmTableSection tableSection = new WasmTableSection();
            tableSection.Tables.Add(new WasmTableEntry { MinElements = (uint)this.Module.Functions.Count + 1, MaxElements = (uint)this.Module.Functions.Count + 1 });
            return tableSection;
        }

        private WasmElementSection TranslateElementSection()
        {
            WasmElementSection elementSection = new WasmElementSection();

            elementSection.Segments.Add(new WasmElementSegment
            {
                Mode = WasmElementSegmentMode.LegacyActive,
                OffsetExpression = [(byte)WasmOpCodes.I32_const, .. LEB128.EncodeSigned(1), (byte)WasmOpCodes.End],
                FunctionIndices = this.Module.Functions.Select(f => f.Offset).ToList(),
            });

            return elementSection;
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
                WasmInstructionEmitter emitter = new WasmInstructionEmitter(this.Module.EntryPoint ?? throw ErrorHandler.Create("Cannot initialize globals if no entrypoint is defined."), this.FactorizedFunctions);

                // A string or array global is two globals, its pointer and its length, which share one initial value.
                byte[] initialValue = global.InitialValue is IRConstantInteger constant
                    ? emitter.EmitScalarConstant(global.ValueType.IsFatPointer ? IRDataTypes.Int32 : global.ValueType.DataType, constant.Value)
                    : emitter.EmitInstruction(global.InitialValue);

                WasmGlobalEntry globalEntry = new WasmGlobalEntry
                {
                    IsMutable = global.IsMutable,
                    Type = WasmTypeTranslator.TranslateIRType(global.ValueType),
                    InitializationExpression = [
                        .. initialValue,
                        (byte)WasmOpCodes.End
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

            exportSection.Exports.Add(new WasmExportEntry
            {
                Name = "__table",
                Kind = WasmExportKind.Table,
                Index = 0
            });

            return exportSection;
        }

        private WasmStartSection TranslateStartSection() // Could use the start sections for entrypoints with 0 returns and 0 parameters,
                                                         // if that saves a couple cycles...
        {
            //if (this.Module.EntryPoint != null)
            //{
            //    if (this.Module.EntryPoint.HasReturn() || this.Module.EntryPoint.HasParameters())
            //    {
            //        throw ErrorHandler.Create($"Entry point \"{this.Module.EntryPoint.Name}\" signature must have exactly 0 returns and 0 parameters!");
            //    }

            //    WasmStartSection startSection = new WasmStartSection()
            //    {
            //        StartFunctionIndex = this.Module.EntryPoint.Offset,
            //        IsSet = true,
            //    };

            //    return startSection;
            //}

            return new WasmStartSection();
        }

        private WasmCodeSection TranslateCodeSection()
        {
            WasmCodeSection codeSection = new WasmCodeSection();

            foreach (IRFunction function in Module.Functions)
            {
                WasmLocalLayout.Assign(function);

                WasmInstructionEmitter instructionEmitter = new WasmInstructionEmitter(function, this.FactorizedFunctions);
                List<byte> bodyBytes = function == this.FactorizedFunctions.Malloc ? this.FactorizedFunctions.EmitMallocBody()
                    : function == this.FactorizedFunctions.Free ? this.FactorizedFunctions.EmitFreeBody()
                    : instructionEmitter.EmitInstructions(function.Entryblock.Instructions);

                // A function that returns a value must not fall off its end. Every path ends in a return, but WebAssembly's
                // validator does not know that a branch that returns on both sides never continues.
                if (function != this.FactorizedFunctions.Malloc && function.ReturnTypes.Any(r => r.DataType != IRDataTypes.Void))
                {
                    bodyBytes.Add((byte)WasmOpCodes.Unreachable);
                }

                bodyBytes.Add((byte)WasmOpCodes.End);

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
            ulong currentMemoryAddress = WasmFactorizedFunctions.DataStart;
            var irStrings = this.Module.Objects.OfType<IRConstantString>().ToList();

            foreach (IRConstantString irString in irStrings)
            {
                List<byte> totalSegmentData = [.. Encoding.UTF8.GetBytes(irString.Value)];

                irString.Offset = currentMemoryAddress;

                List<byte> offsetExpression =
                [
                    (byte)WasmOpCodes.I32_const,
                    .. LEB128.EncodeSigned((int)currentMemoryAddress),
                    (byte)WasmOpCodes.End,
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
