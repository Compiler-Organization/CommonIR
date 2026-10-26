using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRFatPointer : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public bool IsConstant { get; set; } = false;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Pointer { get; set; }

        public IRValueInstruction Length { get; set; }

        public IRType ValueType { get; set; }

        public IRFatPointer(IRValueInstruction pointer, IRValueInstruction length, IRType type)
        {
            if (!type.IsFatPointer)
            {
                throw ErrorHandler.Create($"Cannot create a fat pointer of type '{type.Dump(0)}'.");
            }

            this.Pointer = pointer;
            this.Length = length;
            this.ValueType = type;

            pointer.References.Add(this);
            length.References.Add(this);

            this.Operands.AddRange([pointer, length]);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}fat_pointer ({this.Pointer.Dump(0)}, {this.Length.Dump(0)}) : {this.ValueType.Dump(0)}";
        }
    }
}
