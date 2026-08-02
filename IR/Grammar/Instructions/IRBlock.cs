using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRBlock : IRInstruction
    {
        /// <summary>
        /// The name of the block. Automatically generated if not defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the block. If not defined, the block is set to void.
        /// </summary>
        public IRType ReturnType { get; set; } = new IRType(IRDataTypes.Void);

        /// <summary>
        /// Instructions in the block.
        /// </summary>
        public List<IRInstruction> Instructions { get; set; } = new List<IRInstruction>();
    }
}
