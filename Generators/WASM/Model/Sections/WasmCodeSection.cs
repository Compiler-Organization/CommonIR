namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmCodeSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Code;

        public ulong Size { get; set; } = 0;

        public List<WasmFunctionBody> Functions { get; set; } = new List<WasmFunctionBody>();

        public byte[] Serialize()
        {
            if(this.Functions.Count == 0)
            {
                return [];
            }

            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Functions.Count);

            foreach (WasmFunctionBody function in this.Functions)
            {
                byte[] functionBytes = function.Serialize();
                writer.Write(functionBytes);
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

    internal class WasmFunctionBody
    {
        public List<WasmLocalGroup> Locals { get; set; } = new List<WasmLocalGroup>();

        public List<byte> Instructions { get; set; } = new List<byte>();

        public byte[] Serialize()
        {
            using BinaryWriter bodyWriter = new BinaryWriter();

            bodyWriter.WriteULEB128((ulong)this.Locals.Count);
            foreach (WasmLocalGroup localGroup in this.Locals)
            {
                bodyWriter.WriteULEB128((ulong)localGroup.Count);
                bodyWriter.Write((byte)localGroup.Type);
            }

            foreach (byte opcode in this.Instructions)
            {
                bodyWriter.Write(opcode);
            }

            if (this.Instructions.Count == 0 || this.Instructions[^1] != 0x0B)
            {
                bodyWriter.Write((byte)0x0B);
            }

            byte[] bodyBytes = bodyWriter.GetByteArray();

            using BinaryWriter functionBlockWriter = new BinaryWriter();
            functionBlockWriter.WriteULEB128((ulong)bodyBytes.Length);
            functionBlockWriter.Write(bodyBytes);

            return functionBlockWriter.GetByteArray();
        }
    }

    internal struct WasmLocalGroup
    {
        /// <summary>
        /// How many instances of this local variable type are allocated.
        /// </summary>
        public uint Count { get; set; }

        /// <summary>
        /// The variable data type.
        /// </summary>
        public WasmFormTypes Type { get; set; }
    }
}
