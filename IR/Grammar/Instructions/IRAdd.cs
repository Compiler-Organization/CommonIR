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
    public class IRAdd : IRInstruction
    {
        /// <summary>
        /// The left side of the addition instruction.
        /// </summary>
        public IRGrammar Left { get; set; }

        /// <summary>
        /// The right side of the addition instruction.
        /// </summary>
        public IRGrammar Right { get; set; }

        public IRType Type { get; set; }

        public IRAdd(IRGrammar left, IRGrammar right)
        {
            this.Left = left;
            this.Right = right;

            if(left is IRLocal local)
            {
                this.Type = local.Type;
                return;
            }

            if (left is IRGlobal global)
            {
                this.Type = global.Type;
                return;
            }

            if (left is IRInstruction instruction)
            {
                this.Type = instruction.Type;
                return;
            }

            throw ErrorHandler.Create($"Left in add instruction of type '{left.GetType().FullName}' is not supported.");
        }
    }
}
