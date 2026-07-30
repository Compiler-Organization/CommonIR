using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR
{
    internal class BinaryWriter : System.IO.BinaryWriter
    {
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
    }
}
