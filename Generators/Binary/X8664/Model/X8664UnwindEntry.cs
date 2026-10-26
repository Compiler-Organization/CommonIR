using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    internal class X8664UnwindEntry
    {
        public const int PushOffset = 1;

        public const int FramePointerOffset = 4;

        public int Start { get; set; }

        public int End { get; set; }

        public int FrameSize { get; set; }

        public int PrologSize { get; set; }

        public X8664UnwindEntry(int start, int end, int frameSize, int prologSize)
        {
            this.Start = start;
            this.End = end;
            this.FrameSize = frameSize;
            this.PrologSize = prologSize;
        }

        public byte[] Serialize()
        {
            List<ushort> codes = new List<ushort>();

            if (this.FrameSize > 0)
            {
                if (this.FrameSize <= 128)
                {
                    codes.Add(Code(this.PrologSize, 2, this.FrameSize / 8 - 1));
                }
                else if (this.FrameSize <= 512 * 1024 - 8)
                {
                    codes.Add(Code(this.PrologSize, 1, 0));
                    codes.Add((ushort)(this.FrameSize / 8));
                }
                else
                {
                    codes.Add(Code(this.PrologSize, 1, 1));
                    codes.Add((ushort)(this.FrameSize & 0xFFFF));
                    codes.Add((ushort)(this.FrameSize >> 16));
                }
            }

            codes.Add(Code(FramePointerOffset, 3, 0));
            codes.Add(Code(PushOffset, 0, 5));

            int count = codes.Count;

            if (codes.Count % 2 != 0)
            {
                codes.Add(0);
            }

            List<byte> bytes = [0x01, (byte)this.PrologSize, (byte)count, 0x05];

            foreach (ushort code in codes)
            {
                bytes.AddRange(BitConverter.GetBytes(code));
            }

            return bytes.ToArray();
        }

        static ushort Code(int offset, int operation, int information) => (ushort)(offset | (operation << 8) | (information << 12));
    }
}
