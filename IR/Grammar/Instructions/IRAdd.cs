using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    public class IRAdd : IRInstruction, IRValueInstruction
    {
        /// <summary>
        /// The left side of the addition instruction.
        /// </summary>
        public IRValueInstruction Left { get; set; }

        /// <summary>
        /// The right side of the addition instruction.
        /// </summary>
        public IRValueInstruction Right { get; set; }

        public IRType Type { get; set; }

        public IRAdd(IRValueInstruction left, IRValueInstruction right)
        {
            this.Left = left;
            this.Right = right;

            this.Type = left.Type;
        }
    }
}
