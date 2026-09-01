using CommonIR.Generators.WASM.Model.Sections;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.WASM.Translation
{
    internal class WasmTypeTranslator
    {
        public static WasmFormTypes TranslateIRDataType(IRDataTypes dataType) => dataType switch
        {
            // Reference types
            IRDataTypes.Pointer or
            IRDataTypes.FatPointer or
            IRDataTypes.String or
            IRDataTypes.Array or 
            IRDataTypes.Struct => WasmFormTypes.I32,

            // Scalars
            IRDataTypes.Int32 => WasmFormTypes.I32,
            IRDataTypes.Int64 => WasmFormTypes.I64,
            IRDataTypes.Float32 => WasmFormTypes.F32,
            IRDataTypes.Float64 => WasmFormTypes.F64,
            IRDataTypes.Void => WasmFormTypes.Void,

            _ => throw new NotImplementedException($"Translation for type {dataType} is not implemented.")
        };
        public static WasmFormTypes TranslateIRType(IRType type)
        {
            return TranslateIRDataType(type.DataType);
        }
    }
}
