using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRLoad : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Target { get; set; }

        public IRType ValueType { get; set; }

        public IRLoad(IRValueInstruction target)
        {
            this.Target = target;
            this.ValueType = target.ValueType;

            target.References.Add(this);
        }

        public string Dump()
        {
            return $"load ({this.Target.Dump()})";
        }
    }
}
