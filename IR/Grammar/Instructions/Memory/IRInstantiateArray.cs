using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRInstantiateArray : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();
        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;
        public bool IsConstant { get; set; } = false;

        public IRGrammar? Parent { get; set; }

        public IRArraySchema ArraySchema { get; set; }

        /// <summary>
        /// Explicit element value initializers mapped linearly to array positions.
        /// </summary>
        public List<IRValueInstruction> ElementValues { get; set; } = new List<IRValueInstruction>();

        /// <summary>
        /// A runtime size value required strictly when the underlying ArraySchema is dynamic.
        /// </summary>
        public IRValueInstruction Size { get; set; }

        /// <summary>
        /// The resulting value type produced by this instruction.
        /// </summary>
        public IRType ValueType { get; set; }

        public bool HasDynamicSize => this.Size != null && !this.Size.IsConstant;

        public bool HasFixedSize => this.Size != null && this.Size.IsConstant;

        public bool HasExplicitInitializers => this.ElementValues != null && this.ElementValues.Count > 0;

        /// <summary>
        /// Instantiates a new array from a schema of a dynamic size.
        /// </summary>
        /// <param name="arraySchema"></param>
        /// <param name="size"></param>
        public IRInstantiateArray(IRArraySchema arraySchema, IRValueInstruction size)
        {
            this.ArraySchema = arraySchema;
            this.Size = size;
            this.ValueType = arraySchema.ValueType;

            this.Operands.Add(size);
        }

        /// <summary>
        /// Instantiates a new array from a schema of a dynamic size, with explicit element initializers.
        /// </summary>
        /// <param name="arraySchema"></param>
        /// <param name="size"></param>
        /// <param name="elementValues"></param>
        public IRInstantiateArray(IRArraySchema arraySchema, IRValueInstruction size, List<IRValueInstruction> elementValues)
        {
            this.ArraySchema = arraySchema;
            this.Size = size;
            this.ElementValues = elementValues;
            this.ValueType = arraySchema.ValueType;

            this.Operands.Add(size);

            foreach (var element in elementValues)
            {
                if (element.ValueType != arraySchema.ElementType)
                {
                    throw ErrorHandler.Create($"Type mismatch during array creation. Expected element type '{arraySchema.ElementType}', but got '{element.ValueType}'.");
                }
                this.Operands.Add(element);
            }
        }

        public string Dump(int indentation)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"{new string('\t', indentation)}instantiate_array type=({this.ArraySchema.Dump(0)})");

            if (this.Size != null)
            {
                builder.Append($" runtime_size=({this.Size.Dump(0)})");
            }

            if (this.ElementValues != null && this.ElementValues.Count > 0)
            {
                builder.Append(" elements=[ ");
                for (int i = 0; i < this.ElementValues.Count; i++)
                {
                    builder.Append(this.ElementValues[i].Dump(0));
                    if (i < this.ElementValues.Count - 1)
                    {
                        builder.Append(", ");
                    }
                }
                builder.Append(" ]");
            }
            else
            {
                builder.Append(" elements=default");
            }

            return builder.ToString();
        }
    }
}
