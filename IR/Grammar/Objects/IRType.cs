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
        public IRObject? UserObject { get; set; }

        public IRType(IRDataTypes dataType)
        {
            DataType = dataType;
        }

        public IRType(IRDataTypes dataType, IRObject userObject)
        {
            DataType = dataType;
            UserObject = userObject;
        }

        public string Dump(int indentation)
        {
            if (UserObject != null)
            {
                return $"{new string('\t', indentation)}{DataType}({UserObject.Dump(0)})";
            }
            else
            {
                return $"{new string('\t', indentation)}{DataType.ToString()}";
            }
        }
    }
}
