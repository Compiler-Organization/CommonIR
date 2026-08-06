using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmImportSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Import;

        public ulong Size { get; set; } = 0;

        public List<WasmImport> Imports { get; set; } = new List<WasmImport>();

        public byte[] Serialize()
        {
            if(this.Imports.Count == 0)
            {
                return [];
            }

            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Imports.Count);

            foreach (WasmImport import in this.Imports)
            {
                byte[] moduleBytes = Encoding.UTF8.GetBytes(import.ModuleName ?? string.Empty);
                writer.WriteULEB128((ulong)moduleBytes.Length);
                writer.Write(moduleBytes);

                byte[] fieldBytes = Encoding.UTF8.GetBytes(import.FieldName ?? string.Empty);
                writer.WriteULEB128((ulong)fieldBytes.Length);
                writer.Write(fieldBytes);

                writer.Write((byte)import.Kind);

                switch (import.Kind)
                {
                    case WasmImportKind.Function:
                        writer.WriteULEB128(import.TypeIndex);
                        break;

                    case WasmImportKind.Table:
                        writer.Write((byte)import.TableType);
                        SerializeLimits(writer, import.MinLimits, import.MaxLimits);
                        break;

                    case WasmImportKind.Memory:
                        SerializeLimits(writer, import.MinLimits, import.MaxLimits);
                        break;

                    case WasmImportKind.Global:
                        writer.Write((byte)import.GlobalType);
                        writer.Write((byte)(import.IsGlobalMutable ? 0x01 : 0x00));
                        break;

                    default:
                        throw new NotImplementedException($"Serialization for WasmImportKind '{import.Kind}' is not implemented.");
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

        private void SerializeLimits(BinaryWriter writer, uint min, uint? max)
        {
            if (max.HasValue)
            {
                writer.Write((byte)0x01); // Minimum and maximum
                writer.WriteULEB128(min);
                writer.WriteULEB128(max.Value);
            }
            else
            {
                writer.Write((byte)0x00); // Minimum
                writer.WriteULEB128(min);
            }
        }
    }

    internal class WasmImport
    {
        public required string ModuleName { get; set; }

        public required string FieldName { get; set; }

        public required WasmImportKind Kind { get; set; }

        /// <summary>
        /// The index mapping this import to a specific entry in the Type Section. 
        /// </summary>
        public uint TypeIndex { get; set; }

        /// <summary>
        /// The element type of the table.
        /// </summary>
        public WasmFormTypes TableType { get; set; } = WasmFormTypes.FunctionReference;

        /// <summary>
        /// The value type of the global variable.
        /// </summary>
        public WasmFormTypes GlobalType { get; set; } = WasmFormTypes.I32;

        /// <summary>
        /// Specifies if the imported global variable can be mutated.
        /// </summary>
        public bool IsGlobalMutable { get; set; } = false;

        /// <summary>
        /// The initial/minimum allocation boundary.
        /// </summary>
        public uint MinLimits { get; set; } = 0;

        /// <summary>
        /// The maximum optional allocation boundary.
        /// </summary>
        public uint? MaxLimits { get; set; }
    }

    internal enum WasmImportKind : byte
    {
        Function = 0x00,
        Table = 0x01,
        Memory = 0x02,
        Global = 0x03,
    }
}
