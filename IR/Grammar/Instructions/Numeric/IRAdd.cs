using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions.Arithmetic
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    public class IRAdd : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

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

            this.Operands.AddRange([left, right]);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}add ({this.Left.Dump(0)}), ({this.Right.Dump(0)})";
        }
    }
}
