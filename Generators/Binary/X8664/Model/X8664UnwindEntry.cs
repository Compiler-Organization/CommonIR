using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    internal class X8664UnwindEntry
    {
        public int Start { get; set; }

        public int End { get; set; }

        public int PrologSize { get; set; }

        List<ushort[]> Codes { get; } = new List<ushort[]>();

        public X8664UnwindEntry(int start)
        {
            this.Start = start;
        }

        public void Push(int offset, X8664Register register) => this.Codes.Add([Code(offset, 0, (int)register)]);

        public void SetFramePointer(int offset) => this.Codes.Add([Code(offset, 3, 0)]);

        public void SaveXmm(int offset, X8664XmmRegister register, int stackOffset) => this.Codes.Add([Code(offset, 8, (int)register), (ushort)(stackOffset / 16)]);

        public void Allocate(int offset, int size)
        {
            if (size <= 0)
            {
                return;
            }

            if (size <= 128)
            {
                this.Codes.Add([Code(offset, 2, size / 8 - 1)]);
            }
            else if (size <= 512 * 1024 - 8)
            {
                this.Codes.Add([Code(offset, 1, 0), (ushort)(size / 8)]);
            }
            else
            {
                this.Codes.Add([Code(offset, 1, 1), (ushort)(size & 0xFFFF), (ushort)(size >> 16)]);
            }
        }

        public byte[] Serialize()
        {
            List<ushort> codes = new List<ushort>();

            for (int i = this.Codes.Count - 1; i >= 0; i--)
            {
                codes.AddRange(this.Codes[i]);
            }

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
