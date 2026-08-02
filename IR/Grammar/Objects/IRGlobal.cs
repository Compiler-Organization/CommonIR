using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a global variable at the public scope.
    /// </summary>
    public class IRGlobal : IRObject, IRValueInstruction
    {
        /// <summary>
        /// The name of the global. Automatically generated if not defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the global.
        /// </summary>
        public required IRType Type { get; set; }

        /// <summary>
        /// Determines if the variable can be assigned at any point.
        /// </summary>
        public bool IsMutable { get; set; } = false;

        /// <summary>
        /// Used internally to determine the location of the global variable.
        /// </summary>
        internal ulong Offset { get; set; }
    }
}
