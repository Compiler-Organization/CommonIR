using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Numeric
{
    public class IRBitwise : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRBitwiseOperator Operator { get; set; }

        public IRValueInstruction Left { get; set; }

        public IRValueInstruction Right { get; set; }

        public IRType ValueType { get; set; }

        public IRBitwise(IRBitwiseOperator bitwiseOperator, IRValueInstruction left, IRValueInstruction right)
        {
            this.Operator = bitwiseOperator;
            this.Left = left;
            this.Right = right;

            this.ValueType = left.ValueType;

            left.References.Add(this);
            right.References.Add(this);

            this.Operands.AddRange([left, right]);
        }

        public string Dump(int indentation)
        {
            string op = this.Operator switch
            {
                IRBitwiseOperator.And => "and",
                IRBitwiseOperator.Or => "or",
                IRBitwiseOperator.Xor => "xor",
                IRBitwiseOperator.ShiftLeft => "shl",
                IRBitwiseOperator.ShiftRight => "shr",
                _ => throw ErrorHandler.CreateNotImplimented($"Operator '{this.Operator}' is not implimented in bitwise operations yet")
            };

            return $"{new string('\t', indentation)}bitwise.{op} ({this.Left.Dump(0)}), ({this.Right.Dump(0)})";
        }
    }

    public enum IRBitwiseOperator
    {
        And,
        Or,
        Xor,
        ShiftLeft,
        ShiftRight,
    }
}
