using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRInstantiateStruct : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public bool IsConstant { get; set; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The underlying struct definition tracking fields, offsets, and properties.
        /// </summary>
        public IRStructSchema StructSchema { get; set; }

        /// <summary>
        /// The resulting type of this instruction (typically a pointer to the initialized struct).
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// Explicit values assigned to each field index of the struct respectively.
        /// </summary>
        public List<IRValueInstruction> PropertyValues { get; set; }

        /// <summary>
        /// Instantiates an empty struct from a schema.
        /// </summary>
        /// <param name="structSchema"></param>
        public IRInstantiateStruct(IRStructSchema structSchema)
        {
            this.StructSchema = structSchema;
            this.ValueType = structSchema.ValueType;
            this.PropertyValues = new List<IRValueInstruction>();
        }

        /// <summary>
        /// Instantiates a struct, explicitly initializing its properties values.
        /// </summary>
        /// <param name="structSchema"></param>
        /// <param name="propertyValues"></param>
        public IRInstantiateStruct(IRStructSchema structSchema, List<IRValueInstruction> propertyValues)
        {
            this.StructSchema = structSchema;
            this.PropertyValues = propertyValues;
            this.ValueType = structSchema.ValueType;

            if (propertyValues.Count != structSchema.Properties.Count)
            {
                throw ErrorHandler.Create($"Struct initializer count mismatch for '{structSchema.Name}'. Expected {structSchema.Properties.Count} fields, but got {propertyValues.Count}.");
            }

            for (int i = 0; i < propertyValues.Count; i++)
            {
                if (propertyValues[i].ValueType != structSchema.Properties[i].ValueType)
                {
                    throw ErrorHandler.Create($"Type mismatch initializing field '{structSchema.Properties[i].Name}' on struct '{structSchema.Name}'. Expected type '{structSchema.Properties[i].ValueType}', got '{propertyValues[i].ValueType}'.");
                }
            }

            foreach (IRValueInstruction propertyValue in propertyValues)
            {
                this.Operands.Add(propertyValue);
            }
        }

        public string Dump(int indentation)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{new string('\t', indentation)}init_struct {this.StructSchema.Name} {{ ");

            for (int i = 0; i < PropertyValues.Count; i++)
            {
                sb.Append($".{StructSchema.Properties[i].Name} = {PropertyValues[i].Dump(0)}");
                if (i < PropertyValues.Count - 1) sb.Append(", ");
            }

            sb.Append(" }");
            return sb.ToString();
        }
    }
}