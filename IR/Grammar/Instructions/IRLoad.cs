using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRLoad : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The target of which to load a value. If the target is an IRObject (such as IRLocal, IRGlobal), data will be loaded from their respective locations. If an integer is specified (Such as IRConstantInteger), this will be treated as a memory address and a value will be loaded from that address in memory.
        /// </summary>
        public IRValueInstruction Target { get; set; }

        public IRType ValueType { get; set; }

        public IRLoad(IRValueInstruction target)
        {
            this.Target = target;
            this.ValueType = target.ValueType;

            target.References.Add(this);
            this.Operands.Add(target);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}load ({this.Target.Dump(0)})";
        }
    }
}
