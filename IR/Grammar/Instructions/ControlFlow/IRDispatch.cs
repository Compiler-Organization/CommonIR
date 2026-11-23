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

        public List<IRValueInstruction> Residents { get; set; } = new List<IRValueInstruction>();

        public List<IRStructProperty> ResidentSlots { get; set; } = new List<IRStructProperty>();

        public IRDispatch(IRFunction kernel, IRValueInstruction count, List<IRValueInstruction> arguments)
            : this(kernel, count, arguments, [], [])
        {
        }

        public IRDispatch(IRFunction kernel, IRValueInstruction count, List<IRValueInstruction> arguments, List<IRValueInstruction> residents, List<IRStructProperty> residentSlots)
        {
            if (!kernel.IsKernel)
            {
                throw ErrorHandler.Create($"Cannot dispatch '{kernel.Name}' as it is not a kernel.");
            }

            int expected = kernel.KernelParameters.Count;

            if (expected != arguments.Count)
            {
                throw ErrorHandler.Create($"Kernel '{kernel.Name}' expects {expected} argument(s), but got {arguments.Count}.");
            }

            int arrays = kernel.KernelParameters.Count(p => p.ValueType.DataType == IRDataTypes.Array);

            if (residents.Count != residentSlots.Count || (residents.Count != 0 && residents.Count != arrays))
            {
                throw ErrorHandler.Create($"Kernel '{kernel.Name}' has {arrays} array parameter(s), but the dispatch names {residents.Count} resident array(s).");
            }

            this.Kernel = kernel;
            this.Count = count;
            this.Arguments = arguments;
            this.Residents = residents;
            this.ResidentSlots = residentSlots;

            this.Kernel.References.Add(this);

            count.References.Add(this);
            this.Operands.Add(count);
            this.Operands.AddRange(arguments);
            this.Operands.AddRange(residents);

            foreach (IRValueInstruction argument in arguments.Concat(residents))
            {
                argument.References.Add(this);
            }
        }

        public string Dump(int indentation)
        {
            string residents = this.Residents.Count == 0 ? "" : $" resident ({string.Join(", ", this.Residents.Select(a => a.Dump(0)))})";
            return $"{new string('\t', indentation)}dispatch {this.Kernel.Name} [{this.Count.Dump(0)}] ({string.Join(", ", this.Arguments.Select(a => a.Dump(0)))}){residents}";
        }
    }
}
