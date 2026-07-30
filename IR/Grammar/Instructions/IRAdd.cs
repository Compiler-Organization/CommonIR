using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

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

            throw ErrorHandler.Create($"Left in add instruction of type '{left.GetType().FullName}' is not supported.");
        }
    }
}
