using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Traversal
{
    public class IRLivenessResult
    {
        /// <summary>
        /// The possible routes the value may take
        /// </summary>
        public List<IRBlock> Divergences { get; set; } = new List<IRBlock>();
    }
}
