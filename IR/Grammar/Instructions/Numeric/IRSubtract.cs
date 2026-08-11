using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Arithmetic
{
    /// <summary>
    /// Subtracts right value from the left value
    /// </summary>
    public class IRSubtract : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The left side of the subtraction instruction.
        /// </summary>
        public IRValueInstruction Left { get; set; }

        /// <summary>
        /// The right side of the subtraction instruction.
        /// </summary>
        public IRValueInstruction Right { get; set; }

        public IRType ValueType { get; set; }

        public IRSubtract(IRValueInstruction left, IRValueInstruction right)
        {
            this.Left = left;
            this.Right = right;

            this.ValueType = left.ValueType;

            left.References.Add(this);
            right.References.Add(this);

            this.Operands.AddRange([left, right]);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}subtract ({this.Left.Dump(0)}), ({this.Right.Dump(0)})";
        }
    }
}
