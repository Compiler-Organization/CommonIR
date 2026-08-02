namespace CommonIR
{
    /// <summary>
    /// Helper class for encoding integers using the LEB128 (Little Endian Base 128) encoding scheme.
    /// </summary>
    internal class LEB128
    {
        public static byte[] EncodeUnsigned(ulong value)
        {
            List<byte> bytes = new List<byte>();

            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;

                if (value != 0)
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            }
            while (value != 0);

            return bytes.ToArray();
        }

        public static byte[] EncodeSigned(long value)
        {
            List<byte> bytes = new List<byte>();
            bool more = true;

            while (more)
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;

                if ((value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0))
                {
                    more = false;
                }
                else
                {
                    b |= 0x80;
                }

                bytes.Add(b);
            }

            return bytes.ToArray();
        }
    }
}
