using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public interface IRValueInstruction : IRInstruction
    {
        /// <summary>
        /// The type of the instruction.
        /// </summary>
        IRType Type { get; set; }
    }
}
