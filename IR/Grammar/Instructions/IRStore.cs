using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRStore : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The target of which to store the value. If the target is an IRObject (such as IRLocal, IRGlobal), data will be stored to their respective locations. If an integer is specified (Such as IRConstantInteger), this will be treated as a memory address and the value will be stored at that address in memory.
        /// </summary>
        public IRValueInstruction Target { get; set; }

        /// <summary>
        /// The value to be stored in the target.
        /// </summary>
        public IRValueInstruction Value { get; set; }

        public IRStore(IRValueInstruction target, IRValueInstruction value)
        {
            this.Target = target;
            this.Value = value;

            this.Operands.AddRange([target, value]);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}store ({this.Target.Dump(0)})";
        }
    }
}
