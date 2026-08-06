namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmElementSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Element;

        public ulong Size { get; set; } = 0;

        public List<WasmElementSegment> Segments { get; set; } = new List<WasmElementSegment>();

        public byte[] Serialize()
        {
            if(this.Segments.Count == 0)
            {
                return [];
            }

            using BinaryWriter writer = new BinaryWriter();

            writer.WriteULEB128((ulong)this.Segments.Count);

            foreach (WasmElementSegment segment in this.Segments)
            {
                writer.Write((byte)segment.Mode);

                byte modeByte = (byte)segment.Mode;
                bool isActive = (modeByte & 0x01) == 0 && (modeByte & 0x02) == 0; // 0x00, 0x02, 0x04, 0x06
                bool isDeclarative = modeByte == 0x03 || modeByte == 0x07;
                bool isPassive = modeByte == 0x01 || modeByte == 0x05;

                bool hasExplicitTableIndex = modeByte == 0x02 || modeByte == 0x06;
                bool hasExplicitElemType = modeByte != 0x00; // Omitted ONLY in basic 0x00 legacy mode
                bool usesExpressions = (modeByte & 0x04) != 0; // 0x04, 0x05, 0x06, 0x07

                if (isActive)
                {
                    if (hasExplicitTableIndex)
                    {
                        writer.WriteULEB128(segment.TableIndex);
                    }

                    foreach (byte opcode in segment.OffsetExpression)
                    {
                        writer.Write(opcode);
                    }

                    if (segment.OffsetExpression.Count == 0 || segment.OffsetExpression[^1] != 0x0B)
                    {
                        writer.Write((byte)0x0B);
                    }
                }

                else if (isDeclarative || isPassive)
                {
                    // Structural markers for passive/declarative segments (0x03 or 0x07)
                    // segment type is written below if hasExplicitElemType is true
                }

                if (hasExplicitElemType)
                {
                    if (usesExpressions)
                    {
                        writer.Write((byte)segment.ElemType);
                    }
                    else
                    {
                        writer.Write((byte)0x00);
                    }
                }

                if (usesExpressions)
                {
                    writer.WriteULEB128((ulong)segment.ElementExpressions.Count);
                    foreach (List<byte> expr in segment.ElementExpressions)
                    {
                        foreach (byte opcode in expr)
                        {
                            writer.Write(opcode);
                        }
                        if (expr.Count == 0 || expr[^1] != 0x0B)
                        {
                            writer.Write((byte)0x0B);
                        }
                    }
                }
                else
                {
                    writer.WriteULEB128((ulong)segment.FunctionIndices.Count);
                    foreach (ulong funcIdx in segment.FunctionIndices)
                    {
                        writer.WriteULEB128(funcIdx);
                    }
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

    internal class WasmElementSegment
    {
        public WasmElementSegmentMode Mode { get; set; } = WasmElementSegmentMode.LegacyActive;

        /// <summary>
        /// The target table being populated. Used only if Mode flag specifies dynamic table indices (0x02, 0x06).
        /// </summary>
        public ulong TableIndex { get; set; } = 0;

        /// <summary>
        /// Constant expression determining table offset.
        /// Used only if the segment operates in an Active mode.
        /// </summary>
        public List<byte> OffsetExpression { get; set; } = new List<byte>();

        /// <summary>
        /// The Reference Type of the values inside this element segment. Used only for Expression-based modes.
        /// </summary>
        public WasmFormTypes ElemType { get; set; } = WasmFormTypes.FunctionReference;

        /// <summary>
        /// Collection of target function indices.
        /// </summary>
        public List<ulong> FunctionIndices { get; set; } = new List<ulong>();

        /// <summary>
        /// Collection of structural element expressions.
        /// Used if the segment uses expression-based encoding.
        /// </summary>
        public List<List<byte>> ElementExpressions { get; set; } = new List<List<byte>>();
    }

    internal enum WasmElementSegmentMode : byte
    {
        /// <summary>
        /// Active mode targeting Table 0, parsing raw function indices list. (Wasm 1.0 MVP standard)
        /// </summary>
        LegacyActive = 0x00,

        /// <summary>
        /// Passive mode parsing raw function indices list.
        /// </summary>
        PassiveIndices = 0x01,

        /// <summary>
        /// Active mode targeting an explicit Table index, parsing function indices list.
        /// </summary>
        ActiveExplicitTableIndices = 0x02,

        /// <summary>
        /// Declarative mode parsing raw function indices list.
        /// </summary>
        DeclarativeIndices = 0x03,

        /// <summary>
        /// Active mode targeting Table 0, parsing complex expression elements list.
        /// </summary>
        ActiveExpressions = 0x04,

        /// <summary>
        /// Passive mode parsing complex expression elements list.
        /// </summary>
        PassiveExpressions = 0x05,

        /// <summary>
        /// Active mode targeting an explicit Table index, parsing complex expression elements list.
        /// </summary>
        ActiveExplicitTableExpressions = 0x06,

        /// <summary>
        /// Declarative mode parsing complex expression elements list.
        /// </summary>
        DeclarativeExpressions = 0x07
    }
}
