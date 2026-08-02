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
        /// Instructions in the function.
        /// </summary>
        public List<IRInstruction> Instructions { get; set; } = new List<IRInstruction>();

        /// <summary>
        /// Default entry block appended to the start of the function when initialized.
        /// Function always starts here.
        /// </summary>
        public IRBlock Entryblock { get; set; } = new IRBlock();

        public IRFunction(string name, List<IRLocal> parameters, List<IRType> returnTypes)
        {
            Name = name;
            ReturnTypes = returnTypes;

            foreach (IRLocal parameter in parameters)
            {
                parameter.Offset = (ulong)Parameters.Count;
                this.Parameters.Add(parameter);
            }

            Instructions.Add(Entryblock);
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
        public IRLocal CreateLocal(IRType type, bool isMutable)
        {
            IRLocal local = new IRLocal(type, isMutable);
            local.Offset = (ulong)(Locals.Count + Parameters.Count);
            Locals.Add(local);
            return local;
        }
    }
}
