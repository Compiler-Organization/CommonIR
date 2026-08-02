using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Makes a call to an existing function with given arguments.
    /// </summary>
    public class IRCall : IRInstruction, IRValueInstruction
    {
        /// <summary>
        /// The function being called.
        /// </summary>
        public IRFunction Function { get; set; }

        /// <summary>
        /// The arguments being passed to the function call.
        /// </summary>
        public List<IRValueInstruction> Arguments { get; set; } = new List<IRValueInstruction>();

        public IRType Type { get; set; }

        public IRCall(IRFunction function)
        {
            this.Function = function;
            this.Type = function.ReturnTypes[0]; // TODO: Handle multiple return types
        }

        public IRCall(IRFunction function, List<IRValueInstruction> arguments)
        {
            this.Function = function;
            this.Arguments = arguments;
            this.Type = function.ReturnTypes[0]; // TODO: Handle multiple return types
        }
    }
}
