using CommonIR.Errors;
using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRStructProperty : IRObject, IRValueInstruction
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

        public IRValueInstruction? DefaultValue { get; set; }

        internal FieldInfo? CILField { get; set; }

        public IRStructProperty(IRType type, string name)
        {
            this.ValueType = type;
            this.Name = name;
        }

        public IRStructProperty(IRType type, string name, IRValueInstruction defaultValue)
        {
            this.ValueType = type;
            this.Name = name;
            this.DefaultValue = defaultValue;

            if(type != defaultValue.ValueType)
            {
                throw ErrorHandler.Create($"The default value type '{defaultValue.ValueType}' does not match the property type '{type}'.");
            }
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}struct_property [{this.Index}] ({this.Name})";
        }
    }
}
