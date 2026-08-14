using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a local variable inside a scope.
    /// </summary>
    public class IRLocal : IRObject, IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The name of the local. Automatically generated if nothing is declared.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the local.
        /// </summary>
        public IRType ValueType { get; set; }

        /// <summary>
        /// Determines if the variable can be assigned at any point.
        /// </summary>
        public bool IsMutable { get; set; } = false;

         /// <summary>
         /// If the local is a fat pointer, this is the length companion to it.
         /// </summary>
        public IRLocal? LengthCompanion { get; set; }

        public IRLocal(IRType type, bool isMutable)
        {
            ValueType = type;
            IsMutable = isMutable;
        }

        public IRLocal(string name, IRType type, bool isMutable)
        {
            Name = name;
            ValueType = type;
            IsMutable = isMutable;
        }

        public IRLocal(string name, IRDataTypes type, bool isMutable)
        {
            Name = name;
            ValueType = new IRType(type);
            IsMutable = isMutable;
        }

        /// <summary>
        /// Used internally to determine the location of the local variable.
        /// </summary>
        internal ulong Offset { get; set; }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}local %{this.Name} : {this.ValueType.Dump(0)}";
        }
    }
}
