using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    public class IRAdd : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The left side of the addition instruction.
        /// </summary>
        public IRValueInstruction Left { get; set; }

        /// <summary>
        /// The right side of the addition instruction.
        /// </summary>
        public IRValueInstruction Right { get; set; }

        public IRType ValueType { get; set; }

        public IRAdd(IRValueInstruction left, IRValueInstruction right)
        {
            this.Left = left;
            this.Right = right;

            this.ValueType = left.ValueType;

            left.References.Add(this);
            right.References.Add(this);
        }

        public string Dump()
        {
            return $"add ({this.Left.Dump()}), ({this.Right.Dump()})";
        }
    }
}
