using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRConditionalBranch : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Condition { get; set; }

        public IRBlock ThenBlock { get; set; }

        public IRBlock ElseBlock { get; set; }

        public bool HasElseBlock { get; set; }

        /// <summary>
        /// Executes a block if a condition is met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="thenBlock"></param>
        public IRConditionalBranch(IRValueInstruction condition, IRBlock thenBlock)
        {
            this.Condition = condition;
            this.ThenBlock = thenBlock;
            this.ElseBlock = null!;
            this.HasElseBlock = false;
        }

        /// <summary>
        /// Executes a block if a condition is met, otherwise executes another block.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="thenBlock"></param>
        /// <param name="elseBlock"></param>
        public IRConditionalBranch(IRValueInstruction condition, IRBlock thenBlock, IRBlock elseBlock)
        {
            this.Condition = condition;
            this.ThenBlock = thenBlock;
            this.ElseBlock = elseBlock;
            this.HasElseBlock = true;
        }

        public string Dump()
        {
            return $"branch.conditional ({Condition.Dump()}) {{\n{ThenBlock.Dump()}\n}}";
        }
    }
}
