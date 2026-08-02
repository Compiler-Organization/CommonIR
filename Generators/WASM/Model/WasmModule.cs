using CommonIR.Generators.WASM.Bindings;

namespace CommonIR.Generators.WASM.Model
{
    public class WasmModule
    {
        public byte[] Magic { get; set; } = [0x00, 0x61, 0x73, 0x6D];

        public uint Version { get; set; } = 1;

        public List<WasmSection> Sections { get; set; } = new List<WasmSection>();

        public byte[] Serialize()
        {
            List<byte> wasmData = [.. Magic, .. BitConverter.GetBytes(Version)];

            foreach (var section in Sections)
            {
                byte[] sectionData = section.Serialize();
                wasmData.AddRange(sectionData);
            }

            return [.. wasmData];
        }
    }
}
