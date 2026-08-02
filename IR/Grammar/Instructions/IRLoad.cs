using CommonIR.IR.Grammar.Objects;

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
