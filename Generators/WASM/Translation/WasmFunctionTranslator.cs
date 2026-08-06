using CommonIR.Generators.WASM.Emit;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    internal class WasmFunctionTranslator
    {
        IRModule Module { get; set; }

        public WasmFunctionTranslator(IRModule module)
        {
            this.Module = module;
        }

        public WasmCodeSection TranslateFunctionBodies()
        {
            WasmCodeSection codeSection = new WasmCodeSection();
            WasmInstructionEmitter instructionEmitter = new WasmInstructionEmitter();

            foreach (IRFunction function in Module.Functions)
            {
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

        List<WasmLocalGroup> TranslateLocals(List<IRLocal> locals)
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
    }
}
