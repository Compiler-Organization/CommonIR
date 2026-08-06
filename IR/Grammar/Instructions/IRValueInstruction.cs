using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public interface IRValueInstruction : IRInstruction
    {
        /// <summary>
        /// The type of the instruction.
        /// </summary>
        IRType ValueType { get; set; }

        /// <summary>
        /// All instructions referencing this instruction.
        /// </summary>
        public List<IRInstruction> References { get; set; }
    }
}
