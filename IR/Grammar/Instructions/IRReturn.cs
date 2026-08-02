namespace CommonIR.IR.Grammar.Instructions
{
    public class IRReturn : IRInstruction
    {
        public IRValueInstruction? Value { get; set; }

        /// <summary>
        /// Creates a return instruction with a return value.
        /// </summary>
        /// <param name="value"></param>
        public IRReturn(IRValueInstruction value)
        {
            this.Value = value;
        }

        /// <summary>
        /// Creates a return instruction without a return value.
        /// </summary>
        public IRReturn() { }
    }
}
