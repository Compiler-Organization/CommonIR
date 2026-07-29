using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a local variable inside a scope.
    /// </summary>
    public class IRLocal : IRObject
    {
        /// <summary>
        /// The name of the local. Automatically generated if nothing is declared.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the local.
        /// </summary>
        public required IRType Type { get; set; }

        /// <summary>
        /// Determines if the variable can be assigned at any point.
        /// </summary>
        public bool Mutable { get; set; } = false;
    }
}
