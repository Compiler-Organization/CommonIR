using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    /// <summary>
    /// Makes a call to an existing function with given arguments.
    /// </summary>
    public class IRCall : IRInstruction
    {
        public bool IsVoid { get; }

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The function being called.
        /// </summary>
        public IRFunction Function { get; set; }

        /// <summary>
        /// The arguments being passed to the function call.
        /// </summary>
        public List<IRValueInstruction> Arguments { get; set; } = new List<IRValueInstruction>();

        public IRType ValueType { get; set; }

        public IRCall(IRFunction function)
        {
            this.Function = function;
            this.ValueType = function.ReturnTypes[0]; // TODO: Handle multiple return types
            this.IsVoid = function.ReturnTypes.Count == 0 || function.ReturnTypes[0].DataType == IRDataTypes.Void;
        }

        public IRCall(IRFunction function, List<IRValueInstruction> arguments)
        {
            this.Function = function;
            this.Arguments = arguments;
            this.ValueType = function.ReturnTypes[0]; // TODO: Handle multiple return types
            this.IsVoid = function.ReturnTypes.Count == 0 || function.ReturnTypes[0].DataType == IRDataTypes.Void;

            foreach(IRValueInstruction argument in arguments)
            {
                argument.References.Add(this);
            }
        }

        public string Dump()
        {
            return $"call {this.Function.Name}({string.Join(", ", this.Arguments.Select(a => a.Dump()))})";
        }
    }
}
