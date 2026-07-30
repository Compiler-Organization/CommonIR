using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRLoad : IRInstruction, IRValueInstruction
    {
        public IRValueInstruction Target { get; set; }

        public IRType Type { get; set; }

        public IRLoad(IRValueInstruction target) 
        {
            this.Target = target;
            this.Type = target.Type;
        }
    }
}
