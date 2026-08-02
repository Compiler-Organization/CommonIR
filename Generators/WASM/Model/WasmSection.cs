namespace CommonIR.Generators.WASM.Model
{
    public interface WasmSection
    {
        public WasmSectionIDs ID { get; }

        public ulong Size { get; set; }

        /// <summary>
        /// Converts the section to its binary variant
        /// </summary>
        /// <returns></returns>
        public byte[] Serialize();
    }
}
