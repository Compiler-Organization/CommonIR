using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRBlock : IRObject
    {
        public IRGrammar? Parent { get; set; }
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

        /// <summary>
        /// Represents the speculated amount of logic gates required for the block. Calculated after the target has been emitted.
        /// </summary>
        public ulong SpeculativeCircuitComplexity { get; set; }

        public IRBlock(string name) 
        {
            this.Name = name;
        }

        public IRBlock(string name, List<IRInstruction> instructions) 
        { 
            this.Name = name;
            this.Instructions = instructions;
        }

        public IRBlock(string name, List<IRInstruction> instructions, IRType returnType) 
        {
            this.Name = name;
            this.Instructions = instructions;
            this.ReturnType = returnType;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}block %{Name} (circuit complexity: {this.SpeculativeCircuitComplexity}) \n{new string('\t', indentation)}{{\n{string.Join("\n", Instructions.Select(i => i.Dump(indentation + 1)))}\n{new string('\t', indentation)}}}";
        }
    }
}
