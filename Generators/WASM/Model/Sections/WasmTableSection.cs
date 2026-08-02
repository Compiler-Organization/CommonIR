namespace CommonIR.Generators.WASM.Model.Sections
{
    // Note: In Wasm 1.0 MVP, modules were restricted to a maximum of 1 table instance, but Wasm 2.0 allows multiple tables.
    internal class WasmTableSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Table;

        public ulong Size { get; set; } = 0;

        public List<WasmTableEntry> Tables { get; set; } = new List<WasmTableEntry>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Tables.Count);

            foreach (WasmTableEntry table in this.Tables)
            {
                writer.Write((byte)table.ElementType);

                if (table.MaxElements.HasValue)
                {
                    writer.Write((byte)0x01); // Minimum and maximum
                    writer.WriteULEB128(table.MinElements);
                    writer.WriteULEB128(table.MaxElements.Value);
                }
                else
                {
                    writer.Write((byte)0x00); // Minimum
                    writer.WriteULEB128(table.MinElements);
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

    internal class WasmTableEntry
    {
        /// <summary>
        /// The reference type of elements allowed in this table. 
        /// Defaults to standard funcref (0x70).
        /// </summary>
        public WasmFormTypes ElementType { get; set; } = WasmFormTypes.FunctionReference;

        /// <summary>
        /// The initial/minimum allocation size of the table in elements.
        /// </summary>
        public uint MinElements { get; set; } = 0;

        /// <summary>
        /// The optional maximum limit of elements the table can grow to.
        /// </summary>
        public uint? MaxElements { get; set; }
    }
}
