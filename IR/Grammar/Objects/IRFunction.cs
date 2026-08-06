using CommonIR.IR.Grammar.Instructions;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRFunction : IRObject
    {
        /// <summary>
        /// The name of the function. Automatically generated if nothing is defined.
        /// </summary>
        public string Name { get; set; } = "";


        /// <summary>
        /// The parameters of the function, represented as their respective types.
        /// </summary>
        public List<IRLocal> Parameters { get; set; } = new List<IRLocal>();

        /// <summary>
        /// The type of the value returned in the function.
        /// </summary>
        public List<IRType> ReturnTypes { get; set; }

        /// <summary>
        /// Local variables declared in the function.
        /// </summary>
        public List<IRLocal> Locals { get; set; } = new List<IRLocal>();

        /// <summary>
        /// Default entry block appended to the start of the function when initialized.
        /// Function always starts here.
        /// </summary>
        public IRCodeBlock Entryblock { get; set; }

        public IRGrammar? Parent { get; set; }

        public IRFunction(string name, List<IRLocal> parameters, List<IRType> returnTypes)
        {
            Name = name;
            ReturnTypes = returnTypes;

            this.Entryblock = new IRBlock("<entry>")
            {
                Parent = this
            };

            foreach (IRLocal parameter in parameters)
            {
                parameter.Offset = (ulong)Parameters.Count;
                this.Parameters.Add(parameter);
            }
        }

        /// <summary>
        /// Used for function mapping (WASM, x86, etc.)
        /// </summary>
        internal ulong Offset { get; set; } = 0;

        /// <summary>
        /// Creates a local, adds it to the function and returns it.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isMutable"></param>
        /// <returns></returns>
        public IRLocal CreateLocal(string name, IRType type, bool isMutable)
        {
            IRLocal local = new IRLocal(name, type, isMutable) 
            {
                Parent = this
            };

            local.Offset = (ulong)(Locals.Count + Parameters.Count);
            Locals.Add(local);
            return local;
        }

        public bool HasReturn()
            => this.ReturnTypes.Count > 0 || (this.ReturnTypes.Count == 1 && this.ReturnTypes.First().DataType == IRDataTypes.Void);

        public bool HasParameters()
            => this.Parameters.Count > 0;

        public string Dump()
        {
            return $"function {this.Name}({string.Join(", ", Parameters.Select(p => p.Dump()))}) : ({string.Join(", ", ReturnTypes.Select(r => r.Dump()))}) {{\n{this.Entryblock.Dump()}\n}}";
        }
    }
}
