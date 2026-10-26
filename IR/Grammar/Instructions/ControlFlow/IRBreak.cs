using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRBreak : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}break";
        }
    }
}
