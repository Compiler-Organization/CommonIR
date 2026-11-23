using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    internal class X8664PortableExecutable
    {
        public const int SectionAlignment = 0x1000;

        public const int FileAlignment = 0x200;

        public const long ImageBase = 0x140000000;

        const int HeaderSize = 0x400;

        public byte[] Text { get; set; } = [];

        public byte[] ReadOnlyData { get; set; } = [];

        public byte[] Data { get; set; } = [];

        public int EntryPoint { get; set; }

        public int ImportDirectory { get; set; }

        public int ImportDirectorySize { get; set; }

        public int AddressTable { get; set; }

        public int AddressTableSize { get; set; }

        public int ExceptionDirectory { get; set; }

        public int ExceptionDirectorySize { get; set; }

        public int ThreadDirectory { get; set; }

        public int ThreadDirectorySize { get; set; }

        public int RelocationDirectory { get; set; }

        public int RelocationDirectorySize { get; set; }

        public ushort Subsystem { get; set; } = 3;

        public ulong StackReserve { get; set; } = 0x100000;

        public ulong StackCommit { get; set; } = 0x1000;

        public static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

        public static int TextAddress => SectionAlignment;

        public static int ReadOnlyDataAddress(int textSize) => TextAddress + AlignUp(Math.Max(textSize, 1), SectionAlignment);

        public static int DataAddress(int textSize, int readOnlyDataSize) => ReadOnlyDataAddress(textSize) + AlignUp(Math.Max(readOnlyDataSize, 1), SectionAlignment);

        public byte[] Serialize()
        {
            (string Name, byte[] Data, uint Characteristics)[] sections =
            [
                (".text", this.Text, 0x60000020),
                (".rdata", this.ReadOnlyData, 0x40000040),
                (".data", this.Data, 0xC0000040),
            ];

            using MemoryStream stream = new MemoryStream();
            using System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);

            writer.Write((ushort)0x5A4D);
            writer.Write(new byte[0x3A]);
            writer.Write(0x80);
            writer.Write(new byte[0x80 - 0x40]);

            writer.Write(0x00004550);

            writer.Write((ushort)0x8664);
            writer.Write((ushort)sections.Length);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write((ushort)0xF0);
            writer.Write((ushort)0x0022);

            int imageSize = DataAddress(this.Text.Length, this.ReadOnlyData.Length) + AlignUp(Math.Max(this.Data.Length, 1), SectionAlignment);
            int textRawSize = AlignUp(this.Text.Length, FileAlignment);
            int initializedSize = AlignUp(this.ReadOnlyData.Length, FileAlignment) + AlignUp(this.Data.Length, FileAlignment);

            writer.Write((ushort)0x20B);
            writer.Write((byte)14);
            writer.Write((byte)0);
            writer.Write(textRawSize);
            writer.Write(initializedSize);
            writer.Write(0);
            writer.Write(this.EntryPoint);
            writer.Write(TextAddress);
            writer.Write(ImageBase);
            writer.Write(SectionAlignment);
            writer.Write(FileAlignment);
            writer.Write((ushort)6);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)6);
            writer.Write((ushort)0);
            writer.Write(0);
            writer.Write(imageSize);
            writer.Write(HeaderSize);
            writer.Write(0);
            writer.Write(this.Subsystem);
            writer.Write((ushort)0x8160);
            writer.Write(this.StackReserve);
            writer.Write(this.StackCommit);
            writer.Write(0x100000UL);
            writer.Write(0x1000UL);
            writer.Write(0);
            writer.Write(16);

            for (int i = 0; i < 16; i++)
            {
                (int address, int size) = i switch
                {
                    1 => (this.ImportDirectory, this.ImportDirectorySize),
                    3 => (this.ExceptionDirectory, this.ExceptionDirectorySize),
                    5 => (this.RelocationDirectory, this.RelocationDirectorySize),
                    9 => (this.ThreadDirectory, this.ThreadDirectorySize),
                    12 => (this.AddressTable, this.AddressTableSize),
                    _ => (0, 0)
                };

                writer.Write(address);
                writer.Write(size);
            }

            int[] addresses = [TextAddress, ReadOnlyDataAddress(this.Text.Length), DataAddress(this.Text.Length, this.ReadOnlyData.Length)];
            int rawPointer = HeaderSize;

            for (int i = 0; i < sections.Length; i++)
            {
                byte[] name = new byte[8];
                Encoding.ASCII.GetBytes(sections[i].Name).CopyTo(name, 0);

                int rawSize = AlignUp(sections[i].Data.Length, FileAlignment);

                writer.Write(name);
                writer.Write(Math.Max(sections[i].Data.Length, 1));
                writer.Write(addresses[i]);
                writer.Write(rawSize);
                writer.Write(rawSize == 0 ? 0 : rawPointer);
                writer.Write(0);
                writer.Write(0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write(sections[i].Characteristics);

                rawPointer += rawSize;
            }

            writer.Write(new byte[HeaderSize - (int)stream.Position]);

            foreach ((string _, byte[] data, uint _) in sections)
            {
                writer.Write(data);
                writer.Write(new byte[AlignUp(data.Length, FileAlignment) - data.Length]);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
