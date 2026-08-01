using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR
{
    internal class BinaryWriter : System.IO.BinaryWriter
    {
        private readonly MemoryStream memoryStream;

        // Public parameterless constructor creates the internal MemoryStream
        public BinaryWriter() : this(new MemoryStream()) { }

        // Private constructor hooks up the base class and keeps our reference
        private BinaryWriter(MemoryStream stream) : base(stream)
        {
            memoryStream = stream;
        }

        public void WriteULEB128(ulong value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0)
                {
                    b |= 0x80;
                }
                this.Write(b);
            }
            while (value != 0);
        }

        public byte[] GetByteArray()
        {
            this.Flush();
            return memoryStream.ToArray();
        }
    }
}
