namespace CommonIR.IR.Grammar.Instructions
{
    public interface IRInstruction : IRGrammar
    {
        /// <summary>
        /// Determines if the instruction is value-producing or not.
        /// </summary>
        public bool IsVoid { get; }

        /// <summary>
        /// The parent where this instruction is located
        /// </summary>
        public IRGrammar? Parent { get; set; }

        /// <summary>
        /// Dumps the IR as a string representation of the instruction.
        /// </summary>
        /// <returns></returns>
        public string Dump();
    }
}
