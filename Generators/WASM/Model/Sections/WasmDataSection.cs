namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmDataSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Data;

        public ulong Size { get; set; } = 0;

        public List<WasmDataSegment> Segments { get; set; } = new List<WasmDataSegment>();

        public byte[] Serialize()
        {
            if(this.Segments.Count == 0)
            {
                return [];
            }

            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Segments.Count);

            foreach (WasmDataSegment segment in this.Segments)
            {
                writer.Write((byte)segment.Mode);

                switch (segment.Mode)
                {
                    case WasmDataSegmentMode.ActiveImplicitMemory:
                        foreach (byte opcode in segment.OffsetExpression)
                        {
                            writer.Write(opcode);
                        }

                        if (segment.OffsetExpression.Count == 0 || segment.OffsetExpression[^1] != 0x0B)
                        {
                            writer.Write((byte)0x0B);
                        }
                        break;

                    case WasmDataSegmentMode.Passive:
                        break;

                    case WasmDataSegmentMode.ActiveExplicitMemory:
                        writer.WriteULEB128(segment.MemoryIndex);
                        foreach (byte opcode in segment.OffsetExpression)
                        {
                            writer.Write(opcode);
                        }
                        if (segment.OffsetExpression.Count == 0 || segment.OffsetExpression[^1] != 0x0B)
                        {
                            writer.Write((byte)0x0B);
                        }
                        break;

                    default:
                        throw new NotImplementedException($"Serialization for WasmDataSegmentMode '{segment.Mode}' is not implemented.");
                }

                writer.WriteULEB128((ulong)segment.Data.Count);
                foreach (byte dataByte in segment.Data)
                {
                    writer.Write(dataByte);
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

    internal class WasmDataSegment
    {
        public WasmDataSegmentMode Mode { get; set; } = WasmDataSegmentMode.ActiveImplicitMemory;

        /// <summary>
        /// Used only if Mode == ActiveExplicitMemory.
        /// </summary>
        public ulong MemoryIndex { get; set; } = 0;

        /// <summary>
        /// Used only if the segment is an Active mode variant.
        /// </summary>
        public List<byte> OffsetExpression { get; set; } = new List<byte>();

        /// <summary>
        /// The bytes to be written to memory.
        /// </summary>
        public List<byte> Data { get; set; } = new List<byte>();
    }

    internal enum WasmDataSegmentMode : byte
    {
        /// <summary>
        /// Active segment initializing implicit memory index 0. (Wasm 1.0 MVP standard)
        /// </summary>
        ActiveImplicitMemory = 0x00,

        /// <summary>
        /// Passive segment loaded dynamically by opcodes. (Bulk Memory extension)
        /// </summary>
        Passive = 0x01,

        /// <summary>
        /// Active segment explicitly targeting a specific declared memory index. (Bulk Memory extension)
        /// </summary>
        ActiveExplicitMemory = 0x02
    }
}
