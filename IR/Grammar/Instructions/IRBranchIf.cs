using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRBranchIf : IRInstruction
    {
        /// <summary>
        /// The condition which has to resolve as true in order for the branching to take place.
        /// </summary>
        public required IRGrammar Condition { get; set; }

        /// <summary>
        /// The block being branched to.
        /// </summary>
        public required IRBlock Block { get; set; }

        public IRType Type { get; set; } = new IRType { DataType = IRDataTypes.Void };
    }
}
