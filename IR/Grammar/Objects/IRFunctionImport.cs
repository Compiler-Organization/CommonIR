namespace CommonIR.IR.Grammar.Objects
{
    public class IRFunctionImport : IRFunction, IRObject
    {
        /// <summary>
        /// The module of which the data will be imported
        /// </summary>
        public string Module { get; set; }

        public IRFunctionImport(string module, string name, List<IRLocal> parameters, List<IRType> returnTypes) : base(name, parameters, returnTypes)
        {
            Module = module;
        }

        public new string Dump()
        {
            return $"import module \"{this.Module}\" function \"{this.Name}({string.Join(", ", Parameters.Select(p => p.Dump()))}) : ({string.Join(", ", ReturnTypes.Select(r => r.Dump()))})\"";
        }
    }
}
