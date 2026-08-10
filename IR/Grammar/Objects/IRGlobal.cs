using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a global variable at the public scope.
    /// </summary>
    public class IRGlobal : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The name of the global. Automatically generated if not defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the global.
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// Determines if the variable can be assigned at any point.
        /// </summary>
        public bool IsMutable { get; set; } = false;

        public IRValueInstruction InitialValue { get; set; }

        /// <summary>
        /// Creates a new initialized global.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="valueType"></param>
        /// <param name="isMutable"></param>
        public IRGlobal(string name, IRType valueType, IRValueInstruction initialValue, bool isMutable)
        {
            this.Name = name;
            this.ValueType = valueType;
            this.IsMutable = isMutable;

            this.InitialValue = initialValue;
            this.Operands.Add(initialValue);
        }

        /// <summary>
        /// Used internally to determine the location of the global variable.
        /// </summary>
        internal ulong Offset { get; set; }

        public string DumpDeclaration(int indentation)
        {
            return $"{new string('\t', indentation)}global %{this.Name} : {this.ValueType.Dump(0)} = {this.InitialValue.Dump(0)}";
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}global %{this.Name} : {this.ValueType.Dump(0)}";
        }
    }
}
