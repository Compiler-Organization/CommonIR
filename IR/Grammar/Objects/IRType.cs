using CommonIR.Errors;
using System.Reflection.Metadata.Ecma335;

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
        /// <para>Example: Pointer = 4, FatPointer = 8</para>
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
            or IRDataTypes.Struct => 4,

            IRDataTypes.Array when this.UserObject is IRArray array && array.Size.IsConstant => 4,

            IRDataTypes.FatPointer
            or IRDataTypes.Array
            or IRDataTypes.String => 8,

            _ => throw ErrorHandler.Create($"Cannot get size of {this.DataType} as it is not supported."),
        };

        public bool IsReferenceType
            => this.DataType switch
            {
                IRDataTypes.Pointer
                or IRDataTypes.FatPointer
                or IRDataTypes.String
                or IRDataTypes.Array
                or IRDataTypes.Struct => true,
                _ => false,
            };

        public bool IsFatPointer
            => this.DataType switch
            {
                IRDataTypes.FatPointer
                or IRDataTypes.String => true,

                IRDataTypes.Array when this.UserObject is IRArray array && !array.Size.IsConstant => true,

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

            if (left.UserObject != null && right.UserObject != null && left.UserObject.Equals(right.UserObject)) return true;

            return true;
        }


        public static bool operator !=(IRType? left, IRType? right) => !(left == right);

        public static class Factory
        {
            public static IRType String { get => new IRType(IRDataTypes.String); }
            public static IRType Int8 { get => new IRType(IRDataTypes.Int8); }
            public static IRType UInt8 { get => new IRType(IRDataTypes.UInt8); }
            public static IRType Int16 { get => new IRType(IRDataTypes.Int16); }
            public static IRType UInt16 { get => new IRType(IRDataTypes.UInt16); }
            public static IRType Int32 { get => new IRType(IRDataTypes.Int32); }
            public static IRType UInt32 { get => new IRType(IRDataTypes.UInt32); }
            public static IRType Int64 { get => new IRType(IRDataTypes.Int64); }
            public static IRType Float32 { get => new IRType(IRDataTypes.Float32); }
            public static IRType Float64 { get => new IRType(IRDataTypes.Float64); }
            public static IRType Pointer { get => new IRType(IRDataTypes.Pointer); }
            public static IRType FatPointer { get => new IRType(IRDataTypes.FatPointer); }
            public static IRType Array { get => new IRType(IRDataTypes.Array); }
            public static IRType Struct { get => new IRType(IRDataTypes.Struct); }
            public static IRType Bool { get => new IRType(IRDataTypes.Bool); }
            public static IRType Void { get => new IRType(IRDataTypes.Void); }
            public static IRType Vector { get => new IRType(IRDataTypes.Vector); }
        }
    }
}
