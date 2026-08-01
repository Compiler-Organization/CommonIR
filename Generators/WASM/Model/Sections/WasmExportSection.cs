using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmExportSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Export;

        public ulong Size { get; set; } = 0;

        public List<WasmExportEntry> Exports { get; set; } = new List<WasmExportEntry>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Exports.Count);

            foreach (WasmExportEntry export in this.Exports)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(export.Name ?? string.Empty);

                writer.WriteULEB128((ulong)nameBytes.Length);
                writer.Write(nameBytes);

                writer.Write((byte)export.Kind);

                writer.WriteULEB128(export.Index);
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

    internal class WasmExportEntry
    {
        /// <summary>
        /// The public-facing name identifier used by the host to access this asset.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The category descriptor of the internal module entity being exported.
        /// </summary>
        public WasmExportKind Kind { get; set; } = WasmExportKind.Function;

        /// <summary>
        /// The internal module layout index of the exported entity.
        /// </summary>
        public ulong Index { get; set; } = 0;
    }

    internal enum WasmExportKind : byte
    {
        /// <summary>
        /// Exports a function from the Code/Function index spaces.
        /// </summary>
        Function = 0x00,

        /// <summary>
        /// Exports a definition block from the Table index space.
        /// </summary>
        Table = 0x01,

        /// <summary>
        /// Exports an instance segment from the Memory index space.
        /// </summary>
        Memory = 0x02,

        /// <summary>
        /// Exports a variable configuration from the Global index space.
        /// </summary>
        Global = 0x03,

        /// <summary>
        /// Exports an exception handler configuration from the Tag index space.
        /// </summary>
        Tag = 0x04
    }
}
