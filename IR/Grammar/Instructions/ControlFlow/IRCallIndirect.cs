using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRCallIndirect : IRVoidInstruction, IRValueInstruction
    {
        public bool IsVoid { get; }
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRValueInstruction FunctionPointer { get; set; }

        public List<IRValueInstruction> Arguments { get; set; } = new List<IRValueInstruction>();

        public IRType ValueType { get; set; }

        public IRCallIndirect(IRValueInstruction functionPointer, IRType returnType, List<IRValueInstruction> arguments)
        {
            this.FunctionPointer = functionPointer;
            this.Arguments = arguments;
            this.ValueType = returnType;
            this.IsVoid = returnType.DataType == IRDataTypes.Void;

            functionPointer.References.Add(this);
            this.Operands.Add(functionPointer);
            this.Operands.AddRange(arguments);

            foreach (IRValueInstruction argument in arguments)
            {
                argument.References.Add(this);
            }
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}call.indirect ({this.FunctionPointer.Dump(0)})({string.Join(", ", this.Arguments.Select(a => a.Dump(0)))}) : {this.ValueType.Dump(0)}";
        }
    }
}
