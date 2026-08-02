using CommonIR.Generators.WASM.Model;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmSectionEmitter
    {
        private static readonly byte[] WasmMagic = new byte[] { 0x00, 0x61, 0x73, 0x6D };

        private static readonly byte[] WasmVersion = new byte[] { 0x01, 0x00, 0x00, 0x00 };

        public List<WasmSection> Sections { get; set; } = new List<WasmSection>();

        /// <summary>
        /// Adds a section to the emitter pipeline.
        /// </summary>
        public void AddSection(WasmSection section)
        {
            if (section != null)
            {
                this.Sections.Add(section);
            }
        }

        /// <summary>
        /// Compiles all assigned sections into a fully valid WebAssembly binary module.
        /// </summary>
        public byte[] EmitModule()
        {
            using BinaryWriter moduleWriter = new BinaryWriter();

            moduleWriter.Write(WasmMagic);
            moduleWriter.Write(WasmVersion);

            var orderedSections = this.Sections
                .OrderBy(s => s.ID == WasmSectionIDs.Custom ? int.MaxValue : (int)s.ID)
                .ToList();

            foreach (WasmSection section in orderedSections)
            {
                byte[] sectionBytes = section.Serialize();
                moduleWriter.Write(sectionBytes);
            }

            return moduleWriter.GetByteArray();
        }
    }
}
