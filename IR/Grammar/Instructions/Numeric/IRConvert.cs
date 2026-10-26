using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Numeric
{
    public class IRConvert : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRValueInstruction Value { get; set; }

        public IRType ValueType { get; set; }

        public IRConvert(IRValueInstruction value, IRType targetType)
        {
            this.Value = value;
            this.ValueType = targetType;

            value.References.Add(this);
            this.Operands.Add(value);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}convert ({this.Value.Dump(0)}) -> {this.ValueType.Dump(0)}";
        }
    }
}
