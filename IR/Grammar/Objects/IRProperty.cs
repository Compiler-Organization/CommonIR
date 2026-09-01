using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRProperty : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public int Index { get; set; }

        /// <summary>
        /// The type of the property.
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// The name of the property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The raw offset of the property, given in bytes.
        /// </summary>
        public int Offset { get; set; }

        internal IRProperty(IRType type, string name)
        {
            this.ValueType = type;
            this.Name = name;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}property [{this.Index}] ({this.Name})";
        }
    }
}
