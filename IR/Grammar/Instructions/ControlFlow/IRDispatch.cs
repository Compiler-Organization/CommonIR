using CommonIR.Errors;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.ControlFlow
{
    public class IRDispatch : IRVoidInstruction, IRValueInstruction
    {
        public bool IsVoid { get; } = false;
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }
        public bool IsConstant { get; set; } = false;

        public IRFunction Kernel { get; set; }

        public IRValueInstruction Count { get; set; }

        public List<IRValueInstruction> Arguments { get; set; } = new List<IRValueInstruction>();

        public IRType ValueType { get; set; } = new IRType(IRDataTypes.Pointer);

        public IRDispatch(IRFunction kernel, IRValueInstruction count, List<IRValueInstruction> arguments)
        {
            if (!kernel.IsKernel)
            {
                throw ErrorHandler.Create($"Cannot dispatch '{kernel.Name}' as it is not a kernel.");
            }

            if (kernel.Parameters.Count - 2 != arguments.Count)
            {
                throw ErrorHandler.Create($"Kernel '{kernel.Name}' expects {kernel.Parameters.Count - 2} argument(s), but got {arguments.Count}.");
            }

            this.Kernel = kernel;
            this.Count = count;
            this.Arguments = arguments;

            this.Kernel.References.Add(this);

            count.References.Add(this);
            this.Operands.Add(count);
            this.Operands.AddRange(arguments);

            foreach (IRValueInstruction argument in arguments)
            {
                argument.References.Add(this);
            }
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}dispatch {this.Kernel.Name} [{this.Count.Dump(0)}] ({string.Join(", ", this.Arguments.Select(a => a.Dump(0)))})";
        }
    }
}
