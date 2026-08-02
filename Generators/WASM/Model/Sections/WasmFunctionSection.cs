namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmFunctionSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Function;

        public ulong Size { get; set; } = 0;

        public List<ulong> TypeIndices { get; set; } = new List<ulong>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.TypeIndices.Count);

            foreach (ulong typeIndex in this.TypeIndices)
            {
                writer.WriteULEB128(typeIndex);
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
}
