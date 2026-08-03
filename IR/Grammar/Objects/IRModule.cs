using CommonIR.Errors;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRModule
    {
        public List<IRGlobal> Globals { get; set; }

        public List<IRFunction> Functions { get; set; }

        public List<IRFunctionImport> FunctionImports { get; set; }

        public IRModule()
        {
            this.Globals = new List<IRGlobal>();
            this.Functions = new List<IRFunction>();
            this.FunctionImports = new List<IRFunctionImport>();
        }

        /// <summary>
        /// Creates a function import, adds it to the module and returns it.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="functionName"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public IRFunctionImport CreateFunctionImport(string moduleName, string functionName, IRType returnType, List<IRLocal> parameterTypes)
        {
            IRFunctionImport functionImport = new IRFunctionImport(moduleName, functionName, parameterTypes, new List<IRType> { returnType });
            this.FunctionImports.Add(functionImport);
            return functionImport;
        }

        /// <summary>
        /// Creates a function, adds it to the module and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public IRFunction CreateFunction(string name, IRType returnType, List<IRLocal> parameterTypes)
        {
            IRFunction function = new IRFunction(name, parameterTypes, new List<IRType> { returnType });
            this.Functions.Add(function);
            return function;
        }

        /// <summary>
        /// Creates a global, adds it to the module and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="isMutable"></param>
        /// <returns></returns>
        public IRGlobal CreateGlobal(string name, IRType type, bool isMutable)
        {
            IRGlobal global = new IRGlobal()
            {
                Name = name,
                Type = type,
                IsMutable = isMutable
            };
            this.Globals.Add(global);
            return global;
        }

        /// <summary>
        /// Gets a function declared in the current module. Throws an exception if none was found.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IRFunction GetFunction(string name)
        {
            return this.Functions.FirstOrDefault(f => f.Name == name) ?? throw ErrorHandler.Create($"Function '{name}' not found.");
        }

        /// <summary>
        /// Attempts to get a function declared in the current module. Returns true if found, false otherwise.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="function"></param>
        /// <returns></returns>
        public bool TryGetFunction(string name, out IRFunction function)
        {
            function = Functions.First(f => f.Name == name);
            return function != null;
        }
    }
}
