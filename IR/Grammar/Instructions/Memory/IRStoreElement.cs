using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRStoreElement : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        public IRValueInstruction Target { get; set; }

        public IRValueInstruction Index { get; set; }

        public IRValueInstruction Value { get; set; }

        public IRType ElementType { get; set; }

        public IRStoreElement(IRValueInstruction target, IRValueInstruction index, IRValueInstruction value, IRType elementType)
        {
            this.Target = target;
            this.Index = index;
            this.Value = value;
            this.ElementType = elementType;

            this.Operands.AddRange([target, index, value]);
            target.References.Add(this);
            index.References.Add(this);
            value.References.Add(this);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}store_element ({this.Target.Dump(0)})[{this.Index.Dump(0)}] = ({this.Value.Dump(0)})";
        }
    }
}
