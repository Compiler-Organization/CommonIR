namespace CommonIR.IR.Grammar.Instructions
{
    public class IRConditionalJump : IRInstruction
    {
        /// <summary>
        /// The condition which has to resolve as true in order for the branching to take place.
        /// </summary>
        public IRValueInstruction Condition { get; set; }

        /// <summary>
        /// The block being branched to.
        /// </summary>
        public IRBlock TargetBlock { get; set; }

        public IRConditionalJump(IRValueInstruction condition, IRBlock targetBlock)
        {
            this.Condition = condition;
            this.TargetBlock = targetBlock;
        }
    }
}
