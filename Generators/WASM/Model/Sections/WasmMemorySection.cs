using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmMemorySection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Memory;

        public ulong Size { get; set; } = 0;

        public List<WasmMemoryLimits> Memories { get; set; } = new List<WasmMemoryLimits>(); // Note: In Wasm MVP, modules are limited to a maximum of 1 memory instance.

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Memories.Count);

            foreach (WasmMemoryLimits memory in this.Memories)
            {
                if (memory.MaxPages.HasValue)
                {
                    writer.Write((byte)0x01); // Minimum and maximum
                    writer.WriteULEB128(memory.MinPages);
                    writer.WriteULEB128(memory.MaxPages.Value);
                }
                else
                {
                    writer.Write((byte)0x00); // Minimum
                    writer.WriteULEB128(memory.MinPages);
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

    internal class WasmMemoryLimits
    {
        /// <summary>
        /// The initial/minimum size of the linear memory in units of 64 KiB pages.
        /// </summary>
        public uint MinPages { get; set; } = 1;

        /// <summary>
        /// The optional maximum size the linear memory is allowed to grow to in units of 64 KiB pages.
        /// </summary>
        public uint? MaxPages { get; set; }
    }
}
