using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRStore : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Target { get; set; }

        public IRStore(IRValueInstruction target)
        {
            this.Target = target;
        }

        public string Dump()
        {
            return $"store ({this.Target.Dump()})";
        }
    }
}
