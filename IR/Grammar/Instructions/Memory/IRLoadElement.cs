using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRLoadElement : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public bool IsConstant { get; set; } = false;

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Target { get; set; }

        public IRValueInstruction Index { get; set; }

        public IRType ElementType { get; set; }

        public IRType ValueType { get; set; }

        public IRLoadElement(IRValueInstruction target, IRValueInstruction index, IRType elementType)
        {
            this.Target = target;
            this.Index = index;
            this.ElementType = elementType;
            this.ValueType = elementType;

            target.References.Add(this);
            index.References.Add(this);

            this.Operands.AddRange([target, index]);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}load_element ({this.Target.Dump(0)})[{this.Index.Dump(0)}] : {this.ElementType.Dump(0)}";
        }
    }
}
