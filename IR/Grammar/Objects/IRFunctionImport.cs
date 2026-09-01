using System.Reflection;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRFunctionImport : IRFunction, IRObject
    {
        /// <summary>
        /// The module of which the data will be imported
        /// </summary>
        public string ModuleName { get; set; }

        public new MethodInfo? CILMethod { get; set; }

        public IRFunctionImport(string moduleName, string name, List<IRLocal> parameters, List<IRType> returnTypes) : base(name, parameters, returnTypes, false)
        {
            ModuleName = moduleName;
        }

        public new string Dump(int indentation)
        {
            return $"import module \"{this.ModuleName}\" function \"{this.Name}({string.Join(", ", Parameters.Select(p => p.Dump(0)))}) : ({string.Join(", ", ReturnTypes.Select(r => r.Dump(0)))})\"";
        }
    }
}
