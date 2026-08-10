namespace CommonIR.IR.Grammar.Objects
{
    public interface IRObject : IRGrammar
    {
        /// <summary>
        /// The parent where this object is located
        /// </summary>
        public IRGrammar? Parent { get; set; }

        public string Dump(int indentation);
    }
}
