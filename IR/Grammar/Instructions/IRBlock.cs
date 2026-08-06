using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRBlock : IRCodeBlock, IRVoidInstruction
    {
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
    }
}
