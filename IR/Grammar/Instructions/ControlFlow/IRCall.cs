using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    /// <summary>
    /// Makes a call to an existing function with given arguments.
    /// </summary>
    public class IRCall : IRVoidInstruction, IRValueInstruction
    {
        public bool IsVoid { get; }
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

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
            this.Function.References.Add(this);

            this.IsVoid = function.ReturnTypes.Count == 0 || function.ReturnTypes[0].DataType == IRDataTypes.Void;
            this.ValueType = this.IsVoid ? new IRType(IRDataTypes.Void) : function.ReturnTypes[0]; // TODO: Handle multiple return types
        }

        public IRCall(IRFunction function, List<IRValueInstruction> arguments)
        {
            this.Function = function;
            this.Function.References.Add(this);
            this.Operands.AddRange(arguments);

            this.Arguments = arguments;
            this.IsVoid = function.ReturnTypes.Count == 0 || function.ReturnTypes[0].DataType == IRDataTypes.Void;
            this.ValueType = this.IsVoid ? new IRType(IRDataTypes.Void) : function.ReturnTypes[0]; // TODO: Handle multiple return types

            foreach(IRValueInstruction argument in arguments)
            {
                argument.References.Add(this);
            }
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}call {(this.Function is IRFunctionImport functionImport ?  $"{functionImport.ModuleName}.{functionImport.Name}" : this.Function.Name)}({string.Join(", ", this.Arguments.Select(a => a.Dump(0)))})";
        }
    }
}
