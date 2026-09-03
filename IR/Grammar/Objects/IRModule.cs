using CommonIR.Errors;
using CommonIR.IR.Grammar.Instructions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRModule : IRObject
    {
        public List<IRGlobal> Globals { get; set; }

        public List<IRFunction> Functions { get; set; }

        public List<IRFunctionImport> FunctionImports { get; set; }

        public List<IRObject> Objects { get; set; }

        public ulong ConstantsSize { get; set; } = 0; // TODO: Write a better and more comprehensive calculation of constant size, which will be used when creating a heap pointer for certain architectures.

        public IRFunction? EntryPoint { get; set; }

        public IRGrammar? Parent { get; set; }

        public string Name { get; set; }

        internal ModuleBuilder? CILModule { get; set; }
        internal TypeBuilder? CILType { get; set; }

        public IRModule(string name)
        {
            this.Name = name;
            this.Globals = new List<IRGlobal>();
            this.Functions = new List<IRFunction>();
            this.FunctionImports = new List<IRFunctionImport>();
            this.Objects = new List<IRObject>();
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
            IRFunctionImport functionImport = new IRFunctionImport(moduleName, functionName, parameterTypes, new List<IRType> { returnType })
            {
                Parent = this
            };

            this.FunctionImports.Add(functionImport);
            return functionImport;
        }

        /// <summary>
        /// Attempts to retrieve an exact matching function, if none was found, creates a function import, adds it to the module and returns it.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="functionName"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public IRFunctionImport GetOrCreateFunctionImport(string moduleName, string functionName, IRType returnType, List<IRLocal> parameterTypes)
        {
            List<IRFunctionImport> functionImports = this.FunctionImports.Where(f => 
                f.ModuleName == moduleName
                && f.Name == functionName
                && f.ReturnTypes.All(r => r == returnType)
                && f.Parameters.All(p => parameterTypes.All(pt => pt.ValueType == p.ValueType))
            ).ToList();

            if(functionImports.Count > 0)
            {
                return functionImports.First();
            }

            return CreateFunctionImport(moduleName, functionName, returnType, parameterTypes);
        }

        /// <summary>
        /// Creates a function, adds it to the modules functions and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="returnType"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        public IRFunction CreateFunction(string name, List<IRType> returnTypes, List<IRLocal> parameterTypes, bool isExport)
        {
            IRFunction function = new IRFunction(name, parameterTypes, returnTypes, isExport)
            {
                Parent = this
            };

            this.Functions.Add(function);
            return function;
        }

        /// <summary>
        /// Creates a global, adds it to the modules globals and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="isMutable"></param>
        /// <returns></returns>
        public IRGlobal CreateGlobal(string name, IRType type, IRValueInstruction initialValue, bool isMutable)
        {
            if (type.IsFatPointer)
            {
                IRGlobal aggregatePointer = new IRGlobal($"{name}_ptr", type, initialValue, isMutable)
                {
                    Parent = this,
                    Offset = (ulong)this.Globals.Count
                };
                this.Globals.Add(aggregatePointer);

                IRGlobal lengthCompanion = new IRGlobal($"{name}_len", new IRType(IRDataTypes.Int32), initialValue, isMutable)
                {
                    Parent = this,
                    Offset = (ulong)this.Globals.Count
                };
                this.Globals.Add(lengthCompanion);

                aggregatePointer.LengthCompanion = lengthCompanion;

                return aggregatePointer;
            }
            else
            {
                IRGlobal global = new IRGlobal(name, type, initialValue, isMutable)
                {
                    Parent = this,
                    Offset = (ulong)this.Globals.Count
                };
                this.Globals.Add(global);
                return global;
            }
        }

        /// <summary>
        /// Creates a string, adds it to the modules objects and returns it
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRString CreateString(string value)
        {
            IRString _string = new IRString(value)
            {
                Parent = this,
                //Offset = ConstantsSize
                Offset = (ulong)this.Objects.Count
            };

            this.Objects.Add(_string);

            this.ConstantsSize += (ulong)Encoding.UTF8.GetBytes(value).LongLength;
            //ConstantsSize = (ConstantsSize + (Alignment - 1)) & ~(ulong)(Alignment - 1);

            return _string;
        }

        /// <summary>
        /// Creates an array, adds it to the modules objects and returns it.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public IRArray CreateArray(IRType type, IRValueInstruction size)
        {
            IRArray array = new IRArray(type, size)
            {
                Parent = this,
            };

            this.Objects.Add(array);

            return array;
        }

        /// <summary>
        /// Creates an array with elements, adds it to the modules objects and returns it.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public IRArray CreateArray(IRType type, IRValueInstruction size, List<IRValueInstruction> elements)
        {
            IRArray array = new IRArray(type, size, elements)
            {
                Parent = this,
            };

            this.Objects.Add(array);

            return array;
        }

        /// <summary>
        /// Creates a named struct with no properties ,adds it to the moduels constants and returns it.
        /// </summary>
        /// <returns></returns>
        public IRStruct CreateStruct(string name)
        {
            IRStruct _struct = new IRStruct(name, new List<IRStructProperty>())
            {
                Parent = this,
            };
            this.Objects.Add(_struct);

            return _struct;
        }

        /// <summary>
        /// Creates a named struct with the given properties, adds it to the modules constants and returns it.
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public IRStruct CreateStruct(string name, List<IRStructProperty> properties)
        {
            IRStruct _struct = new IRStruct(name, properties) 
            {
                Parent = this,
            };
            this.Objects.Add(_struct);

            return _struct;
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

        public string Dump(int indentation)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Module:");
            foreach (var global in Globals)
            {
                builder.AppendLine(global.DumpDeclaration(0));
            }

            builder.AppendLine();

            foreach (var functionImport in FunctionImports)
            {
                builder.AppendLine(functionImport.Dump(0));
            }

            builder.AppendLine();

            foreach (var function in Functions)
            {
                builder.AppendLine(function.Dump(0));
            }

            return builder.ToString();
        }

    }
}
