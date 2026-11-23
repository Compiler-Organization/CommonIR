using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Numeric
{
    public enum IRMathOperation
    {
        SquareRoot,
        Reinterpret,
    }

    public class IRMath : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRMathOperation Operation { get; set; }

        public IRValueInstruction Value { get; set; }

        public IRType ValueType { get; set; }

        public IRMath(IRMathOperation operation, IRValueInstruction value)
        {
            this.Operation = operation;
            this.Value = value;
            this.ValueType = value.ValueType;

            value.References.Add(this);
            this.Operands.Add(value);
        }

        public IRMath(IRMathOperation operation, IRValueInstruction value, IRType valueType) : this(operation, value)
        {
            this.ValueType = valueType;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}{this.Operation.ToString().ToLowerInvariant()} ({this.Value.Dump(0)}) -> {this.ValueType.Dump(0)}";
        }
    }
}
