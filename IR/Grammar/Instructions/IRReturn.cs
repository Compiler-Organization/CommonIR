namespace CommonIR.IR.Grammar.Instructions
{
    public class IRReturn : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

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
            this.Operands.Add(value);
        }

        /// <summary>
        /// Creates a return instruction without a return value.
        /// </summary>
        public IRReturn() { }

        public string Dump(int indentation)
        {
            if (this.Value != null)
            {
                return $"{new string('\t', indentation)}return ({this.Value.Dump(0)})";
            }
            else
            {
                return $"{new string('\t', indentation)}return";
            }
        }
    }
}
