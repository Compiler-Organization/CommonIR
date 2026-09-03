using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.Generators.CIL.Emission
{
    internal class CILScratchPool
    {
        private readonly IRFunction Function;
        private readonly ILGenerator ILGenerator;

        private readonly Dictionary<Type, Queue<LocalBuilder>> FreePool = new();
        private readonly HashSet<LocalBuilder> TrackedLocals = new();
        private int TotalRegistered = 0;

        public CILScratchPool(IRFunction function)
        {
            if(function.CILMethod == null)
            {
                throw ErrorHandler.Create($"Function '{function.Name}' does not have an associated CIL method");
            }

            this.Function = function;
            this.ILGenerator = function.CILMethod.GetILGenerator();
        }

        public LocalBuilder Borrow(Type type)
        {
            if (!FreePool.TryGetValue(type, out var queue))
            {
                queue = FreePool[type] = new Queue<LocalBuilder>();
            }

            if (queue.Count > 0)
            {
                return queue.Dequeue();
            }

            string uniqueName = $"$reg_{type.ToString().ToLower()}_{TotalRegistered++}";
            LocalBuilder newLocal = ILGenerator.DeclareLocal(type);

            TrackedLocals.Add(newLocal);
            return newLocal;
        }

        public void Return(LocalBuilder local)
        {
            if (local == null) return;

            if (!TrackedLocals.Contains(local))
            {
                throw ErrorHandler.Create($"Local at index '{local.LocalIndex}' does not exist in scratch pool");
            }

            FreePool[local.LocalType].Enqueue(local);
        }
    }
}
