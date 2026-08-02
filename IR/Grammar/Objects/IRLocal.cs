using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a local variable inside a scope.
    /// </summary>
    public class IRLocal : IRObject, IRValueInstruction
    {
        /// <summary>
        /// The name of the local. Automatically generated if nothing is declared.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the local.
        /// </summary>
        public IRType Type { get; set; }

        /// <summary>
        /// Determines if the variable can be assigned at any point.
        /// </summary>
        public bool IsMutable { get; set; } = false;

        public IRLocal(IRType type, bool isMutable)
        {
            Type = type;
            IsMutable = isMutable;
        }

        public IRLocal(string name, IRType type, bool isMutable)
        {
            Name = name;
            Type = type;
            IsMutable = isMutable;
        }

        public IRLocal(string name, IRDataTypes type, bool isMutable)
        {
            Name = name;
            Type = new IRType(type);
            IsMutable = isMutable;
        }

        /// <summary>
        /// Used internally to determine the location of the local variable.
        /// </summary>
        internal ulong Offset { get; set; }
    }
}
