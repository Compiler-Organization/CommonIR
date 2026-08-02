using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    internal class WasmTypeTranslator
    {
        public static WasmFormTypes TranslateIRDataType(IRDataTypes dataType)
        {
            switch (dataType)
            {
                case IRDataTypes.Int32:
                    return WasmFormTypes.I32;
                case IRDataTypes.Int64:
                    return WasmFormTypes.I64;
                case IRDataTypes.Float32:
                    return WasmFormTypes.F32;
                case IRDataTypes.Float64:
                    return WasmFormTypes.F64;
                case IRDataTypes.Void:
                    return WasmFormTypes.Void;
                default:
                    throw new NotImplementedException($"Translation for type {dataType} is not implemented.");
            }
        }
        public static WasmFormTypes TranslateIRType(IRType type)
        {
            return TranslateIRDataType(type.DataType);
        }
    }
}
