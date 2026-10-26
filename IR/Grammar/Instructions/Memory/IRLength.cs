using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRLength : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public bool IsConstant { get; set; } = false;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Target { get; set; }

        public IRType ValueType { get; set; } = new IRType(IRDataTypes.Int32);

        public IRLength(IRValueInstruction target)
        {
            this.Target = target;

            target.References.Add(this);
            this.Operands.Add(target);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}length ({this.Target.Dump(0)})";
        }
    }
}
