using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Model.Sections
{
    internal class WasmImportSection : WasmSection
    {
        public WasmSectionIDs ID { get; } = WasmSectionIDs.Import;

        public ulong Size { get; set; } = 0;

        public List<WasmImport> Imports { get; set; } = new List<WasmImport>();
    }

    internal class WasmImport
    {
        public required string ModuleName { get; set; }

        public required string FieldName { get; set; }

        public required WasmImportKind Kind { get; set; }

        /// <summary>
        /// The signature that this import maps to
        /// </summary>
        public required uint MappedSignature { get; set; }
    }

    internal enum WasmImportKind
    {
        Function = 0x00,
        Table = 0x01,
        Memory = 0x02,
        Global = 0x03,
    }
}
