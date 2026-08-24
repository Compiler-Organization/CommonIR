using CommonIR.Errors;

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

        /// <summary>
        /// Returns the amount of bytes required for the type.
        /// <para>Returns 4 if the type is non-scalar (pointer)</para>
        /// </summary>
        /// <returns></returns>
        public int Width => this.DataType switch
        {
            IRDataTypes.Int8
            or IRDataTypes.UInt8
            or IRDataTypes.Bool => 1,

            IRDataTypes.Int16
            or IRDataTypes.UInt16 => 2,

            IRDataTypes.Int32
            or IRDataTypes.UInt32 => 4,

            IRDataTypes.Int64
            or IRDataTypes.UInt64 => 8,

            IRDataTypes.Float32 => 4,
            IRDataTypes.Float64 => 8,

            IRDataTypes.Pointer
            or IRDataTypes.String
            or IRDataTypes.Array
            or IRDataTypes.UserObject => 8,

            _ => throw ErrorHandler.Create($"Cannot get size of {this.DataType} as it is not supported."),
        };

        public bool IsReferenceType
            => this.DataType switch
            {
                IRDataTypes.Pointer
                or IRDataTypes.String
                or IRDataTypes.Array
                or IRDataTypes.UserObject => true,
                _ => false,
            };

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

        public static bool operator ==(IRType? left, IRType? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;

            if (left.DataType != right.DataType) return false;

            return left.UserObject != null
                && right.UserObject != null
                && left.UserObject.Equals(right.UserObject);
        }


        public static bool operator !=(IRType? left, IRType? right) => !(left == right);
    }
}
