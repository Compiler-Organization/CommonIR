using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    /// <summary>
    /// Defines a global variable at the public scope.
    /// </summary>
    public class IRGlobal : IRObject
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
        public bool Mutable { get; set; } = false;
    }
}
