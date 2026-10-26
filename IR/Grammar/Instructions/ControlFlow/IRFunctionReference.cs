using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRFunctionReference : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        public bool IsConstant { get; set; } = true;

        public IRFunction Function { get; set; }

        public IRType ValueType { get; set; } = new IRType(IRDataTypes.Pointer);

        public IRFunctionReference(IRFunction function)
        {
            this.Function = function;
            this.Function.References.Add(this);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}function_reference {this.Function.Name}";
        }
    }
}
