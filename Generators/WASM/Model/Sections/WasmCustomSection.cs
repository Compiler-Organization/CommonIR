using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmCustomSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Custom;

        public ulong Size { get; set; } = 0;

        public string Name { get; set; } = string.Empty;

        public List<byte> Data { get; set; } = new List<byte>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            byte[] nameBytes = Encoding.UTF8.GetBytes(this.Name ?? string.Empty);

            writer.WriteULEB128((ulong)nameBytes.Length);
            writer.Write(nameBytes);

            writer.Write(this.Data.ToArray());

            byte[] payloadBytes = writer.GetByteArray();
            this.Size = (ulong)payloadBytes.Length;

            using BinaryWriter sectionWriter = new BinaryWriter();

            sectionWriter.Write((byte)this.ID);
            sectionWriter.WriteULEB128(this.Size);
            sectionWriter.Write(payloadBytes);

            return sectionWriter.GetByteArray();
        }
    }
}
