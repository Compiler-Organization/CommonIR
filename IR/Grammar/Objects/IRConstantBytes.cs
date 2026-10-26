using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRConstantBytes : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = true;

        public byte[] Value { get; set; }

        public IRType ValueType { get; set; }

        public IRConstantBytes(byte[] value, IRType valueType)
        {
            this.Value = value;
            this.ValueType = valueType;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}bytes[{this.Value.Length}]";
        }
    }
}
