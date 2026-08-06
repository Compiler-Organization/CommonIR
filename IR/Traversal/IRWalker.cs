using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Traversal
{
    public class IRWalker
    {
        public static List<IRInstruction> WalkOperands(IRInstruction instruction)
        {
            return instruction switch
            {
                IRStore store => [store.Target],
                IRCall call => [.. call.Arguments],
                IRAdd add => [add.Left, add.Right],
                IRLoad load => [load.Target],
                IRReturn ret => ret.Value is not null ? [ret.Value] : [],

                _ => []
            };
        }


    }
}
