using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Numeric
{
    public class IRCompare : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

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

            this.Operands.AddRange([left, right]);
        }

        public string Dump(int indentation)
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

            return $"{new string('\t', indentation)}compare ({this.Left.Dump(0)}){op}({this.Right.Dump(0)})";
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
