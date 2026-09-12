using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRArraySchema : IRObject
    {
        // Type schemas are global metadata definitions; they do not have operational dependencies
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();
        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = true; // Type definitions are always constant

        /// <summary>
        /// The base IR primitive type indicator (e.g., IRDataTypes.Array).
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// The type of the elements stored within the array.
        /// </summary>
        public IRType ElementType { get; set; }

        internal IRArraySchema(IRType elementType)
        {
            this.ValueType = new IRType(IRDataTypes.Array, this);
            this.ElementType = elementType;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}array_schema : [{this.ElementType.Dump(0)}]";
        }
    }
}