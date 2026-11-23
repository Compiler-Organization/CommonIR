using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommonIR.Generators.Binary.Xtensa.Model
{
    internal sealed class Esp32Image
    {
        public const byte Magic = 0xE9;

        public const ushort ChipEsp32S3 = 9;

        public uint EntryPoint { get; set; }

        public byte FlashMode { get; set; } = 2;

        public byte FlashSizeAndFrequency { get; set; } = 0x4F;

        public List<(uint Address, byte[] Data)> Segments { get; } = new List<(uint Address, byte[] Data)>();

        public byte[] Serialize()
        {
            using MemoryStream stream = new MemoryStream();
            using System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);

            writer.Write(Magic);
            writer.Write((byte)this.Segments.Count);
            writer.Write(this.FlashMode);
            writer.Write(this.FlashSizeAndFrequency);
            writer.Write(this.EntryPoint);

            writer.Write((byte)0xEE);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(ChipEsp32S3);
            writer.Write((byte)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0xFFFF);
            writer.Write(0u);
            writer.Write((byte)1);

            byte checksum = 0xEF;

            foreach ((uint address, byte[] data) in this.Segments)
            {
                byte[] padded = data;

                if (data.Length % 4 != 0)
                {
                    padded = new byte[(data.Length + 3) & ~3];
                    data.CopyTo(padded, 0);
                }

                writer.Write(address);
                writer.Write((uint)padded.Length);
                writer.Write(padded);

                foreach (byte value in padded)
                {
                    checksum ^= value;
                }
            }

            while (stream.Position % 16 != 15)
            {
                writer.Write((byte)0);
            }

            writer.Write(checksum);
            writer.Flush();

            byte[] image = stream.ToArray();
            byte[] digest = SHA256.HashData(image);

            return [.. image, .. digest];
        }
    }
}
