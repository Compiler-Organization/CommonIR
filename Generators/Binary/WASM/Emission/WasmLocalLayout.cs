using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Generators.Binary.WASM.Emission
{
    /// <summary>
    /// Assigns WebAssembly local indices. Parameters come first and a string or array parameter takes two slots (its pointer
    /// and its length), then the function's locals in declaration order, length companions included.
    /// </summary>
    internal static class WasmLocalLayout
    {
        public static void Assign(IRFunction function)
        {
            ulong index = 0;

            foreach (IRLocal parameter in function.Parameters)
            {
                parameter.Offset = index++;

                if (parameter.ValueType.IsFatPointer && parameter.LengthCompanion != null)
                {
                    parameter.LengthCompanion.Offset = index++;
                }
            }

            foreach (IRLocal local in function.Locals)
            {
                local.Offset = index++;
            }
        }

        /// <summary>
        /// Creates a local while the function is being emitted. Locals are only ever appended, so the indices
        /// already written into the emitted code stay valid.
        /// </summary>
        public static IRLocal CreateLocal(IRFunction function, string name, IRType type)
        {
            IRLocal local = function.CreateLocal(name, type, isMutable: true);
            Assign(function);
            return local;
        }
    }
}
