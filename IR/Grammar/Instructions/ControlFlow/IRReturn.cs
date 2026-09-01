namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRReturn : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public List<IRValueInstruction>? Values { get; set; }

        /// <summary>
        /// Creates a return instruction with a return value.
        /// </summary>
        /// <param name="value"></param>
        public IRReturn(List<IRValueInstruction> values)
        {
            this.Values = values;

            foreach(IRValueInstruction value in values)
            {
                value.References.Add(this);
                this.Operands.Add(value);
            }
        }

        /// <summary>
        /// Creates a return instruction without a return value.
        /// </summary>
        public IRReturn() { }

        public string Dump(int indentation)
        {
            if (this.Values != null)
            {
                return $"{new string('\t', indentation)}return {string.Join(", ", $"({this.Values.Select(v => v.Dump(0))})")}";
            }
            else
            {
                return $"{new string('\t', indentation)}return";
            }
        }
    }
}
