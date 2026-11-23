using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions.Memory
{
    /// <summary>
    /// Releases memory returned by <see cref="IRMalloc"/>, or allocated by <see cref="IRInstantiateArray"/>,
    /// <see cref="IRInstantiateStruct"/> or a dispatch. Freeing null or the address of constant data (such as a string
    /// constant) does nothing, so a value that may or may not be on the heap can always be freed.
    /// </summary>
    public class IRFree : IRVoidInstruction
    {
        /// <summary>
        /// When a module has an Int64 global with this name, the back ends keep the number of allocations that have
        /// not been freed in it, so a program can check that it released everything.
        /// </summary>
        public const string LiveAllocationsGlobal = "__heap_live";

        public bool IsVoid { get; } = true;

        public List<IRInstruction> Operands { get; set; } = new List<IRInstruction>();

        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// The address of the memory to release.
        /// </summary>
        public IRValueInstruction Pointer { get; set; }

        public IRFree(IRValueInstruction pointer)
        {
            this.Pointer = pointer;
        }

        public string Dump(int indentation)
        {
            return $"{new string('\t', indentation)}free ({this.Pointer.Dump(0)})";
        }
    }
}
