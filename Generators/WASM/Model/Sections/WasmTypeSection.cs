using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmTypeSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Type;

        public ulong Size { get; set; } = 0;

        List<WasmTypeSectionType> Types { get; set; } = new List<WasmTypeSectionType>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Types.Count);

            foreach (WasmTypeSectionType type in this.Types)
            {
                writer.Write((byte)type.Form);

                switch (type.Form)
                {
                    case WasmForms.Function:
                        writer.WriteULEB128((ulong)type.Parameters.Count);
                        foreach (WasmFormTypes parameterType in type.Parameters)
                        {
                            writer.Write((byte)parameterType);
                        }

                        writer.WriteULEB128((ulong)type.Returns.Count);
                        foreach (WasmFormTypes returnType in type.Returns)
                        {
                            writer.Write((byte)returnType);
                        }
                        break;

                    case WasmForms.GCStruct:
                        writer.WriteULEB128((ulong)type.StructFields.Count);
                        foreach (var field in type.StructFields)
                        {
                            writer.Write((byte)field.Type);
                            writer.Write((byte)(field.IsMutable ? 0x01 : 0x00));
                        }
                        break;

                    case WasmForms.GCArrayType:
                        writer.Write((byte)type.ArrayElementType);
                        writer.Write((byte)(type.IsArrayElementMutable ? 0x01 : 0x00));
                        break;

                    default:
                        throw new NotImplementedException($"Serialization for WasmForm '{type.Form}' is not implemented yet.");
                }
            }

            byte[] payloadBytes = writer.GetByteArray();
            this.Size = (ulong)payloadBytes.Length;

            using BinaryWriter sectionWriter = new BinaryWriter();

            sectionWriter.Write((byte)this.ID);
            sectionWriter.WriteULEB128(this.Size);
            sectionWriter.Write(payloadBytes);

            return sectionWriter.GetByteArray();
        }
    }

    internal class WasmTypeSectionType
    {
        public WasmForms Form { get; set; } = WasmForms.Function;

        // Used if Form == WasmForms.Function
        /// <summary>
        /// Type definitions of the parameters
        /// </summary>
        public List<WasmFormTypes> Parameters { get; set; } = new List<WasmFormTypes>();

        /// <summary>
        /// Type definitions of returned values
        /// </summary>
        public List<WasmFormTypes> Returns { get; set; } = new List<WasmFormTypes>();

        // Used if Form == WasmForms.GCStruct
        public List<WasmStructField> StructFields { get; set; } = new List<WasmStructField>();

        // Used if Form == WasmForms.GCArrayType
        public WasmFormTypes ArrayElementType { get; set; }
        public bool IsArrayElementMutable { get; set; }
    }

    internal struct WasmStructField
    {
        public WasmFormTypes Type { get; set; }
        public bool IsMutable { get; set; }
    }

    internal enum WasmForms
    {
        Function = 0x60,

        /// <summary>
        /// GC feature extension
        /// </summary>
        GCStruct = 0x5F,

        /// <summary>
        /// GC feature extension
        /// </summary>
        GCArrayType = 0x5E,

        /// <summary>
        /// GC feature extension
        /// </summary>
        SubtypeDefinitionDeclaration = 0x4E,

        /// <summary>
        /// Control opcodes
        /// </summary>
        EmptyBlockStructural = 0x40,
    }

    internal enum WasmFormTypes
    {
        /// <summary>
        /// 32-bit integer
        /// </summary>
        I32 = 0x7F,

        /// <summary>
        /// 64-bit integer
        /// </summary>
        I64 = 0x7E,

        /// <summary>
        /// 32-bit single-precision floating point
        /// </summary>
        F32 = 0x7D,

        /// <summary>
        /// 64-bit single-precision floating point
        /// </summary>
        F64 = 0x7C,

        /// <summary>
        /// 128-bit vector of packed integers or floats
        /// </summary>
        V128 = 0x7B,

        /// <summary>
        /// Untyped reference to a function
        /// </summary>
        FunctionReference = 0x70,

        /// <summary>
        /// Opaque reference to a host-managed object (e.g JS object)
        /// </summary>
        ExternReference = 0x6F,

        /// <summary>
        /// Reference to a thrown exception (Exception Handling proposal)
        /// </summary>
        ExceptionReference = 0x69,
    }
}
