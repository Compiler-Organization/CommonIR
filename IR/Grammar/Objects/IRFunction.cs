using CommonIR.IR.Grammar.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRFunction : IRObject
    {
        /// <summary>
        /// The name of the function. Automatically generated if nothing is defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the value returned in the function.
        /// </summary>
        public required IRType ReturnType { get; set; }

        /// <summary>
        /// Local variables declared in the function.
        /// </summary>
        public List<IRLocal> Locals { get; set; } = new List<IRLocal>();

        /// <summary>
        /// Instructions in the function.
        /// </summary>
        public List<IRInstruction> Instructions { get; set; } = new List<IRInstruction>();
    }
}
