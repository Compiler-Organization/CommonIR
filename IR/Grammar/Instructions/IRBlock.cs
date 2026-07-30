using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRBlock : IRInstruction
    {
        /// <summary>
        /// The name of the block. Automatically generated if not defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Instructions in the block.
        /// </summary>
        public List<IRInstruction> Instructions { get; set; } = new List<IRInstruction>();
    }
}
