namespace CommonIR.IR.Grammar.Instructions
{
    public class IRJump : IRInstruction
    {
        public IRBlock TargetBlock { get; set; }

        public IRJump(IRBlock targetBlock)
        {
            this.TargetBlock = targetBlock;
        }
    }
}
