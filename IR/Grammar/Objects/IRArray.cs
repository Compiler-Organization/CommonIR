using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRArray : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        /// <summary>
        /// The type of the array.
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// The instruction declaring the size of the array. If the specified size is a constant, the array will be initialized in the applications data.
        /// </summary>
        public IRValueInstruction Size { get; set; }

        /// <summary>
        /// The elements declared in the array.
        /// </summary>
        public List<IRValueInstruction> Elements { get; set; }

        /// <summary>
        /// The type of the declared elements.
        /// </summary>
        public IRType ElementType { get; set; }

        public IRArray(IRType type, IRValueInstruction size)
        {
            this.ValueType = new IRType(IRDataTypes.Array, this);

            this.ElementType = type;
            this.Size = size;
            this.Elements = new List<IRValueInstruction>();

            size.References.Add(this);
            this.Operands.Add(size);
        }

        public IRArray(IRType type, IRValueInstruction size, List<IRValueInstruction> elements)
        {
            this.ValueType = new IRType(IRDataTypes.Array, this);
            this.ElementType = type;

            this.Size = size;
            this.Elements = elements;

            this.Operands.Add(size);

            size.References.Add(this);
            foreach (IRValueInstruction valueInstruction in elements)
            {
                valueInstruction.References.Add(this);
                this.Operands.Add(valueInstruction);
            }
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}array : {this.ValueType.Dump(0)} {{ {(this.Elements.Count > 0 ? string.Join(", ", this.Elements.Select(e => e.Dump(0))) : "")} }}";
        }
    }
}
