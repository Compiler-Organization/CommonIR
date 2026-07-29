using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public interface IRInstruction : IRGrammar
    {
        /// <summary>
        /// The type of the instruction.
        /// </summary>
        IRType Type { get; set; }
    }
}
