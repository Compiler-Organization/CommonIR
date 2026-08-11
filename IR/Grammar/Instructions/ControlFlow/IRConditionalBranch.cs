using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRConditionalBranch : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

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

            condition.References.Add(this);
            this.Operands.Add(condition);
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

            condition.References.Add(this);
            this.Operands.Add(condition);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}branch.conditional ({Condition.Dump(0)}) \n{new string('\t', indentation)}{{\n{ThenBlock.Dump(indentation + 1)}\n{(ElseBlock == null ? "" : ElseBlock.Dump(indentation + 1))}\n{new string('\t', indentation)}}}";
        }
    }
}
