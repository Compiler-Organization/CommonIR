using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions.Memory
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

        /// <summary>
        /// The offset to the loaded off the target. If the offset is not specified, loads the target itself.
        /// </summary>
        public IRValueInstruction? Offset { get; set; }

        public IRType ValueType { get; set; }

        public IRLoad(IRValueInstruction target)
        {
            this.Target = target;
            this.ValueType = target.ValueType;

            target.References.Add(this);
            this.Operands.Add(target);
        }

        public IRLoad(IRValueInstruction target, IRValueInstruction offset)
        {
            this.Target = target;
            this.Offset = offset;
            this.ValueType = offset.ValueType;

            target.References.Add(this);
            offset.References.Add(this);

            this.Operands.Add(target);
            this.Operands.Add(offset);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}load ({this.Target.Dump(0)})";
        }
    }
}
