using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Traversal
{
    public class IRWalker
    {
        public static List<IRInstruction> WalkOperands(IRValueInstruction instruction)
        {
            return instruction.References;
        }
    }
}
