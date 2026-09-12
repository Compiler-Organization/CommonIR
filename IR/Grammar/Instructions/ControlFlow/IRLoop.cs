using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRLoop : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Condition { get; set; }

        public IRBlock Block { get; set; }

        public IRLoop(IRValueInstruction condition, IRBlock block) 
        {
            this.Condition = condition;
            this.Block = block;

            this.Operands.Add(condition);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}loop ({this.Condition.Dump(0)}) \n{new string('\t', indentation)}{{\n{this.Block.Dump(indentation + 1)}\n{new string('\t', indentation)}}}";
        }
    }
}
