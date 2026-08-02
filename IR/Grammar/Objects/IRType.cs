namespace CommonIR.IR.Grammar.Objects
{
    public class IRType : IRGrammar
    {
        /// <summary>
        /// The datatype definition of type.
        /// </summary>
        public IRDataTypes DataType { get; set; }

        /// <summary>
        /// The user object defined in the type, if any.
        /// </summary>
        public IRGrammar? UserObject { get; set; }

        public IRType(IRDataTypes dataType)
        {
            DataType = dataType;
        }

        public IRType(IRDataTypes dataType, IRGrammar userObject)
        {
            DataType = dataType;
            UserObject = userObject;
        }
    }
}
