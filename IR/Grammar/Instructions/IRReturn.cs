namespace CommonIR.IR.Grammar.Instructions
{
    public class IRReturn : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction? Value { get; set; }

        /// <summary>
        /// Creates a return instruction with a return value.
        /// </summary>
        /// <param name="value"></param>
        public IRReturn(IRValueInstruction value)
        {
            this.Value = value;

            value.References.Add(this);
        }

        /// <summary>
        /// Creates a return instruction without a return value.
        /// </summary>
        public IRReturn() { }

        public string Dump()
        {
            if (this.Value != null)
            {
                return $"return ({this.Value.Dump()})";
            }
            else
            {
                return "return";
            }
        }
    }
}
