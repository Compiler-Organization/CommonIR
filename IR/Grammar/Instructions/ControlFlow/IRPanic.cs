using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRPanic : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Message { get; set; }

        public IRPanic(IRValueInstruction message)
        {
            this.Message = message;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}panic ({Message.Dump(0)})";
        }
    }
}
