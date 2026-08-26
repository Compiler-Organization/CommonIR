using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRFunction : IRObject
    {
        /// <summary>
        /// The name of the function. Automatically generated if nothing is defined.
        /// </summary>
        public string Name { get; set; } = "";

        public bool IsExport { get; set; }

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
        /// Blocks readily available in the function.
        /// </summary>
        public List<IRBlock> Blocks { get; set; } = new List<IRBlock>();

        /// <summary>
        /// Default entry block appended to the start of the function when initialized.
        /// Function always starts here.
        /// </summary>
        public IRBlock Entryblock { get; set; }

        public IRGrammar? Parent { get; set; }

        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public IRFunction(string name, List<IRLocal> parameters, List<IRType> returnTypes, bool isExport)
        {
            Name = name;

            this.Entryblock = new IRBlock("<entry>")
            {
                Parent = this
            };

            this.Blocks.Add(this.Entryblock);

            var flattenedReturnTypes = new List<IRType>();
            foreach (var retType in returnTypes)
            {
                if (retType.IsReferenceType)
                {
                    flattenedReturnTypes.Add(new IRType(IRDataTypes.Int32));
                    flattenedReturnTypes.Add(new IRType(IRDataTypes.Int32));
                }
                else
                {
                    flattenedReturnTypes.Add(retType);
                }
            }
            ReturnTypes = flattenedReturnTypes;

            foreach (IRLocal parameter in parameters)
            {
                if (parameter.ValueType.IsReferenceType)
                {
                    IRLocal aggregatePointer = new IRLocal($"{parameter.Name}_ptr", parameter.ValueType, parameter.IsMutable)
                    {
                        Offset = (ulong)this.Parameters.Count
                    };
                    this.Parameters.Add(aggregatePointer);

                    IRLocal lengthCompanion = new IRLocal($"{parameter.Name}_len", new IRType(IRDataTypes.Int32), parameter.IsMutable)
                    {
                        Offset = (ulong)this.Parameters.Count
                    };
                    this.Parameters.Add(lengthCompanion);

                    aggregatePointer.LengthCompanion = lengthCompanion;
                }
                else
                {
                    parameter.Offset = (ulong)this.Parameters.Count;
                    this.Parameters.Add(parameter);
                }
            }

            IsExport = isExport;
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
                Parent = this,
                Offset = (ulong)(Locals.Count + Parameters.Count)
            };
            Locals.Add(local);

            if (type.IsReferenceType)
            {
                local.LengthCompanion = new IRLocal(new IRType(IRDataTypes.Int32), isMutable: true)
                {
                    Parent = local,
                    Offset = (ulong)(Locals.Count + Parameters.Count),
                };
                Locals.Add(local.LengthCompanion);
            }

            return local;
        }

        /// <summary>
        /// Attempts to find an exact match, if none, creates a new local with the given parameters and adds it to the function.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="isMutable"></param>
        /// <returns></returns>
        public IRLocal GetOrCreateLocal(string name, IRType type, bool isMutable)
        {
            List<IRLocal> locals = this.Locals.Where(l => l.Name == name && l.ValueType == type && l.IsMutable == isMutable).ToList();
            if(locals.Count > 0)
            {
                return locals.First();
            }

            return CreateLocal(name, type, isMutable);
        }

        /// <summary>
        /// Creates a block and adds it to the functions block pool.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IRBlock CreateBlock(string name)
        {
            IRBlock block = new IRBlock(name) 
            {
                Parent = this
            };

            this.Blocks.Add(block);
            return block;
        }

        public bool HasReturn()
            => this.ReturnTypes.Count > 0 || (this.ReturnTypes.Count == 1 && this.ReturnTypes.First().DataType == IRDataTypes.Void);

        public bool HasParameters()
            => this.Parameters.Count > 0;

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}{(this.IsExport ? "export " : "")}function [{this.Offset}] {this.Name}({string.Join(", ", Parameters.Select(p => p.Dump(0)))}) : ({string.Join(", ", ReturnTypes.Select(r => r.Dump(0)))}) \n{new string('\t', indentation)}{{\n{this.Entryblock.Dump(indentation + 1)}\n{new string('\t', indentation)}}}";
        }
    }
}
