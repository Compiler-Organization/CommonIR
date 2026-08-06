using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRConditionalBlock : IRCodeBlock, IRVoidInstruction
    {
        /// <summary>
        /// Executes the block if the condition is met.
        /// </summary>
        public IRValueInstruction Condition { get; set; }

        public IRConditionalBlock(string name, IRValueInstruction condition)
        {
            this.Name = name;
            this.Condition = condition;
        }

        public IRConditionalBlock(string name, List<IRInstruction> instructions, IRValueInstruction condition)
        {
            this.Name = name;
            this.Instructions = instructions;
            this.Condition = condition;
        }

        public IRConditionalBlock(string name, List<IRInstruction> instructions, IRType returnType, IRValueInstruction condition)
        {
            this.Name = name;
            this.Instructions = instructions;
            this.ReturnType = returnType;
            this.Condition = condition;
        }

        public new string Dump()
        {
            string innerContent = string.Join("\n", Instructions.Select(i => i.Dump()));

            string indentedContent = string.Join("\n", innerContent
                .Split('\n')
                .Select(line => $"\t{line}"));

            return $"block.conditional %{Name} ({this.Condition.Dump()}) {{\n{indentedContent}\n}}";
        }
    }
}