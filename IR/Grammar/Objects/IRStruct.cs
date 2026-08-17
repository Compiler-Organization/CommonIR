using CommonIR.Errors;
using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRStruct : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The type of the struct.
        /// </summary>
        public IRType ValueType { get; set; }

        public List<IRProperty> Properties { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// The width of the struct, given in bytes.
        /// </summary>
        public int Width { get; set; }

        public IRStruct(string name, List<IRProperty> properties)
        {
            this.Name = name;
            this.ValueType = new IRType(IRDataTypes.UserObject, this);
            this.Properties = new List<IRProperty>();

            foreach(IRProperty property in properties)
            {
                AddProperty(property);
            }
        }

        /// <summary>
        /// Adds a pre-defined property to the struct, sets its parent to itself and assigns an index.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        public IRProperty AddProperty(IRProperty property)
        {
            property.Index = this.Properties.Count;
            property.Offset = this.Width;
            this.Width += property.ValueType.Width;

            if(property.Parent != null)
            {
                throw ErrorHandler.Create($"Cannot add property '{property.Name}' to struct '{this.Name}' as the property is already used somewhere else.");
            }

            property.Parent = this;
            this.Properties.Add(property);
            return property;
        }

        /// <summary>
        /// Adds a new property to the struct, sets its parent to itself and assigns an index.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public IRProperty AddProperty(IRType type, string name)
        {
            IRProperty property = new IRProperty(type, name)
            {
                Parent = this,
                Index = this.Properties.Count,
                Offset = this.Width
            };

            this.Width += property.ValueType.Width;

            this.Properties.Add(property);

            return property;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)} struct";
        }
    }
}
