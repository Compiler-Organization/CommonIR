using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRString : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = true;

        /// <summary>
        /// The value of the string literal
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The type of the string.
        /// </summary>
        public IRType ValueType { get; set; }

        public IRString(string value)
        {
            this.Value = value;
            this.ValueType = new IRType(IRDataTypes.FatPointer);
        }

        /// <summary>
        /// Used internally to determine the location of the string.
        /// </summary>
        internal ulong Offset { get; set; }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}\"{this.Value}\"";
        }
    }
}
