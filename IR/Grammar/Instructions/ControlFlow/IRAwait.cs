using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRAwait : IRVoidInstruction, IRValueInstruction
    {
        public bool IsVoid { get; } = false;
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRValueInstruction Handle { get; set; }

        public IRType ValueType { get; set; }

        public IRAwait(IRValueInstruction handle, IRType resultType)
        {
            this.Handle = handle;
            this.ValueType = resultType;

            handle.References.Add(this);
            this.Operands.Add(handle);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}await ({this.Handle.Dump(0)}) : {this.ValueType.Dump(0)}";
        }
    }
}
