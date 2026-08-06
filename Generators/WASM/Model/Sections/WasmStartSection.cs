namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmStartSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Start;

        public ulong Size { get; set; } = 0;

        public ulong StartFunctionIndex { get; set; } = 0;

        public bool IsSet { get; set; } = false;

        public byte[] Serialize()
        {
            if(!IsSet)
            {
                return [];
            }

            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128(this.StartFunctionIndex);

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
