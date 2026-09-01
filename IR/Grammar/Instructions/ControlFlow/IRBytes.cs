using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRBytes : IRVoidInstruction, IRValueInstruction
    {
        public bool IsVoid { get; set; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public byte[] Bytes { get; set; }

        public bool IsConstant { get; set; } = false;

        public IRType ValueType { get; set; }

        /// <summary>
        /// Creates a new injection of raw bytes.
        /// </summary>
        /// <param name="bytes">The bytes to be injected</param>
        /// <param name="type">The type of the return value (void if none).</param>
        public IRBytes(byte[] bytes, IRType type) 
        {
            this.Bytes = bytes;
            this.ValueType = type;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}bytes -> {string.Join(" ", this.Bytes.Select(b => $"0x{b.ToString("X2")}"))}";
        }
    }
}
