using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    public class IRStore : IRVoidInstruction
    {
        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The target of which to store the value. If the target is an IRObject (such as IRLocal, IRGlobal), data will be stored to their respective locations. If an integer is specified (Such as IRConstantInteger), this will be treated as a memory address and the value will be stored at that address in memory.
        /// </summary>
        public IRValueInstruction Target { get; set; }

        /// <summary>
        /// The offset of a store should be a property or a number. If the offset is a property, the target is expected to be a pointer to the object.
        /// </summary>
        public IRValueInstruction? Offset { get; set; }

        /// <summary>
        /// The value to be stored in the target.
        /// </summary>
        public IRValueInstruction Value { get; set; }

        public IRStore(IRValueInstruction target, IRValueInstruction value)
        {
            this.Target = target;
            this.Value = value;

            this.Operands.AddRange([target, value]);
            target.References.Add(this);
            value.References.Add(this);
        }

        public IRStore(IRValueInstruction target, IRValueInstruction offset, IRValueInstruction value)
        {
            this.Target = target;
            this.Offset = offset;
            this.Value = value;

            this.Operands.AddRange([target, offset, value]);
            target.References.Add(this);
            offset.References.Add(this);
            value.References.Add(this);
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}store ({this.Target.Dump(0)}){(this.Offset == null ? "" : $" -> {this.Offset.Dump(0)}")} = ({this.Value.Dump(0)})";
        }
    }
}
