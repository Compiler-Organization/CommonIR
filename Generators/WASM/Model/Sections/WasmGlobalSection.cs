using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmGlobalSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Global;

        public ulong Size { get; set; } = 0;

        public List<WasmGlobalEntry> Globals { get; set; } = new List<WasmGlobalEntry>();

        public byte[] Serialize()
        {
            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Globals.Count);

            foreach (WasmGlobalEntry global in this.Globals)
            {
                writer.Write((byte)global.Type);
                writer.Write((byte)(global.IsMutable ? 0x01 : 0x00));

                foreach (byte opcode in global.InitializationExpression)
                {
                    writer.Write(opcode);
                }

                if (global.InitializationExpression.Count == 0 || global.InitializationExpression[^1] != 0x0B)
                {
                    writer.Write((byte)0x0B);
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

    internal class WasmGlobalEntry
    {
        /// <summary>
        /// The scalar value or reference type of this global variable.
        /// </summary>
        public WasmFormTypes Type { get; set; } = WasmFormTypes.I32;

        /// <summary>
        /// Indicates whether the value of this global can be changed.
        /// </summary>
        public bool IsMutable { get; set; } = false;

        /// <summary>
        /// Constant instructions calculating the initial value.
        /// </summary>
        public List<byte> InitializationExpression { get; set; } = new List<byte>();
    }
}
