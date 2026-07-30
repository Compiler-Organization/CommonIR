using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Performs a branch to the given block.
    /// </summary>
    public class IRBranch : IRInstruction
    {
        /// <summary>
        /// The block being branched to.
        /// </summary>
        public required IRBlock Block { get; set; }
    }
}
