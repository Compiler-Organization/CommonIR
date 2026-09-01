using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    /// <summary>
    /// Retrieves the size of an object as Int32.
    /// </summary>
    public class IRSizeOf : IRValueInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        /// <summary>
        /// The target to retrieve the size of.
        /// </summary>
        public IRGrammar Target { get; set; }

        public IRType ValueType { get; set; } = new IRType(IRDataTypes.Int32);

        public IRSizeOf(IRGrammar target)
        {
            this.Target = target;
        }

        public string Dump(int indendation)
        {
            return $"{new string('\t', indendation)}sizeof ({this.Target})";
        }
    }
}
