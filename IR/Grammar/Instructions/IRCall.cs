using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Makes a call to an existing function with given arguments.
    /// </summary>
    public class IRCall : IRInstruction
    {
        /// <summary>
        /// The function being called.
        /// </summary>
        public IRFunction Function { get; set; }

        /// <summary>
        /// The arguments being passed to the function call.
        /// </summary>
        public List<IRGrammar>? Arguments { get; set; }

        public IRType Type { get; set; }

        public IRCall(IRFunction function)
        {
            this.Function = function;
            this.Type = function.ReturnType;
        }
    }
}
