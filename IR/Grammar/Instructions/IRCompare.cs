using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRCompare : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The left side of the comparison instruction.
        /// </summary>
        public IRValueInstruction Left { get; set; }

        /// <summary>
        /// The right side of the comparison instruction.
        /// </summary>
        public IRValueInstruction Right { get; set; }

        public IRComparisonOperator Operator { get; set; }

        public IRType ValueType { get; set; }

        public IRCompare(IRComparisonOperator comparisonOperator, IRValueInstruction left, IRValueInstruction right)
        {
            this.Operator = comparisonOperator;
            this.Left = left;
            this.Right = right;

            this.ValueType = left.ValueType;

            left.References.Add(this);
            right.References.Add(this);
        }

        public string Dump()
        {
            string op = this.Operator switch
            {
                IRComparisonOperator.EqualTo => " == ",
                IRComparisonOperator.NotEqualTo => " => ",

                IRComparisonOperator.LessThan => " < ",
                IRComparisonOperator.LessThanOrEqual => " <= ",
                IRComparisonOperator.GreaterThan => " > ",
                IRComparisonOperator.GreaterThanOrEqual => " >= ",

                _ => throw ErrorHandler.CreateNotImplimented($"Operator '{this.Operator}' is not implimented in comparsions yet")
            };

            return $"compare ({this.Left.Dump()}){op}({this.Right.Dump()})";
        }
    }

    public enum IRComparisonOperator
    {
        EqualTo,
        NotEqualTo,

        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }
}
