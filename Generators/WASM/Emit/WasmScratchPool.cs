using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.WASM.Emit
{
    internal class WasmScratchPool
    {
        private readonly IRFunction Function;

        private readonly Dictionary<IRDataTypes, Queue<IRLocal>> FreePool = new();
        private readonly HashSet<IRLocal> TrackedLocals = new();
        private int TotalRegistered = 0;

        public WasmScratchPool(IRFunction function)
        {
            this.Function = function;
            FreePool[IRDataTypes.Int32] = new Queue<IRLocal>();
            FreePool[IRDataTypes.Int64] = new Queue<IRLocal>();
        }

        public IRLocal Borrow(IRDataTypes dataType)
        {
            if (!FreePool.TryGetValue(dataType, out var queue))
            {
                throw ErrorHandler.CreateNotImplimented($"Scratch pool for type '{dataType}' is not supported.");
            }

            if (queue.Count > 0)
            {
                return queue.Dequeue();
            }

            string uniqueName = $"$reg_{dataType.ToString().ToLower()}_{TotalRegistered++}";
            IRLocal newLocal = Function.CreateLocal(uniqueName, new IRType(dataType), isMutable: true);

            TrackedLocals.Add(newLocal);
            return newLocal;
        }

        public void Return(IRLocal local)
        {
            if (local == null) return;

            if (!TrackedLocals.Contains(local))
            {
                throw ErrorHandler.Create($"Local '{local.Name}' does not exist in scratch pool");
            }

            FreePool[local.ValueType.DataType].Enqueue(local);
        }
    }
}
