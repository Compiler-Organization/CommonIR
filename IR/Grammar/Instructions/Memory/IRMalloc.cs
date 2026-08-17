using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRMalloc : IRValueInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The amount of bytes to be allocated
        /// </summary>
        public IRValueInstruction Bytes { get; set; }

        public IRType ValueType { get; set; } = new IRType(IRDataTypes.Int32);

        public IRMalloc(IRValueInstruction bytes)
        {
            this.Bytes = bytes;
        }

        public string Dump(int indendation)
        {
            return $"{new string('\t', indendation)}malloc ({this.Bytes.Dump(0)})";
        }
    }
}
