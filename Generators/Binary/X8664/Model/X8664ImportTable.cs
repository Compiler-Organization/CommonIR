using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Model
{
    internal class X8664ImportTable
    {
        List<(string Library, List<string> Functions)> Libraries { get; } = new List<(string Library, List<string> Functions)>();

        Dictionary<(string Library, string Function), X8664Symbol> Slots { get; } = new Dictionary<(string Library, string Function), X8664Symbol>();

        public int Offset { get; private set; }

        public int Size { get; private set; }

        public int DescriptorsOffset => this.Offset;

        public int DescriptorsSize => (this.Libraries.Count + 1) * 20;

        public int AddressTableOffset { get; private set; }

        public int AddressTableSize { get; private set; }

        public X8664Symbol Add(IRFunctionImport functionImport)
        {
            string library = functionImport.ModuleName.ToLowerInvariant();

            if (!library.EndsWith(".dll", StringComparison.Ordinal))
            {
                throw ErrorHandler.Create($"Cannot import '{functionImport.ModuleName}.{functionImport.Name}': only functions from Windows libraries (.dll) can be imported when targeting x86-64.");
            }

            if (this.Slots.TryGetValue((library, functionImport.Name), out X8664Symbol? slot))
            {
                return slot;
            }

            int index = this.Libraries.FindIndex(l => l.Library == library);

            if (index == -1)
            {
                this.Libraries.Add((library, new List<string>()));
                index = this.Libraries.Count - 1;
            }

            this.Libraries[index].Functions.Add(functionImport.Name);

            slot = new X8664Symbol($"__imp_{functionImport.Name}", X8664Sections.Data);
            this.Slots[(library, functionImport.Name)] = slot;

            return slot;
        }

        public void Layout(int offset)
        {
            this.Offset = offset;

            int position = offset + this.DescriptorsSize;

            foreach ((string _, List<string> functions) in this.Libraries)
            {
                position += (functions.Count + 1) * 8;
            }

            this.AddressTableOffset = position;

            foreach ((string library, List<string> functions) in this.Libraries)
            {
                foreach (string function in functions)
                {
                    this.Slots[(library, function)].Offset = position;
                    position += 8;
                }

                position += 8;
            }

            this.AddressTableSize = position - this.AddressTableOffset;

            foreach ((string _, List<string> functions) in this.Libraries)
            {
                foreach (string function in functions)
                {
                    position += AlignUp(2 + Encoding.ASCII.GetByteCount(function) + 1, 2);
                }
            }

            foreach ((string library, List<string> _) in this.Libraries)
            {
                position += Encoding.ASCII.GetByteCount(library) + 1;
            }

            this.Size = AlignUp(position, 8) - offset;
        }

        static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

        public byte[] Serialize(int sectionAddress)
        {
            byte[] bytes = new byte[this.Size];

            int Relative(int position) => position - this.Offset;
            void WriteInt32(int position, int value) => BitConverter.GetBytes(value).CopyTo(bytes, Relative(position));
            void WriteInt64(int position, long value) => BitConverter.GetBytes(value).CopyTo(bytes, Relative(position));

            int lookupPosition = this.Offset + this.DescriptorsSize;
            int namePosition = this.AddressTableOffset + this.AddressTableSize;

            List<int> lookupTables = new List<int>();
            Dictionary<(string Library, string Function), int> hints = new Dictionary<(string Library, string Function), int>();

            foreach ((string library, List<string> functions) in this.Libraries)
            {
                lookupTables.Add(lookupPosition);
                lookupPosition += (functions.Count + 1) * 8;
            }

            foreach ((string library, List<string> functions) in this.Libraries)
            {
                foreach (string function in functions)
                {
                    hints[(library, function)] = namePosition;

                    byte[] name = Encoding.ASCII.GetBytes(function);
                    name.CopyTo(bytes, Relative(namePosition + 2));
                    namePosition += AlignUp(2 + name.Length + 1, 2);
                }
            }

            List<int> libraryNames = new List<int>();

            foreach ((string library, List<string> _) in this.Libraries)
            {
                libraryNames.Add(namePosition);

                byte[] name = Encoding.ASCII.GetBytes(library);
                name.CopyTo(bytes, Relative(namePosition));
                namePosition += name.Length + 1;
            }

            for (int i = 0; i < this.Libraries.Count; i++)
            {
                (string library, List<string> functions) = this.Libraries[i];
                int descriptor = this.Offset + i * 20;

                WriteInt32(descriptor, sectionAddress + lookupTables[i]);
                WriteInt32(descriptor + 12, sectionAddress + libraryNames[i]);
                WriteInt32(descriptor + 16, sectionAddress + this.Slots[(library, functions[0])].Offset);

                for (int j = 0; j < functions.Count; j++)
                {
                    long hint = sectionAddress + hints[(library, functions[j])];

                    WriteInt64(lookupTables[i] + j * 8, hint);
                    WriteInt64(this.Slots[(library, functions[j])].Offset, hint);
                }
            }

            return bytes;
        }
    }
}
