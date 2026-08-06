namespace CommonIR.IR.Grammar
{
    public enum IRDataTypes
    {
        /// <summary>
        /// Lets the compiler itself completely decide the type.
        /// </summary>
        Any,

        /// <summary>
        /// Void type, aka nothing.
        /// </summary>
        Void,

        /// <summary>
        /// One byte, manages only one bit in a byte (e.g 00000001 == true, 00000000 == false)
        /// </summary>
        Bool,

        /// <summary>
        /// One byte signed integer scalar.
        /// </summary>
        Int8,

        /// <summary>
        /// One byte unsigned integer scalar.
        /// </summary>
        UInt8,

        /// <summary>
        /// Two byte signed integer scalar.
        /// </summary>
        Int16,

        /// <summary>
        /// Two byte unsigned integer scalar.
        /// </summary>
        UInt16,

        /// <summary>
        /// Four-byte signed integer scalar.
        /// </summary>
        Int32,

        /// <summary>
        /// Four-byte unsigned integer scalar.
        /// </summary>
        UInt32,

        /// <summary>
        /// Eight byte signed integer scalar.
        /// </summary>
        Int64,

        /// <summary>
        /// Eight byte unsigned integer scalar.
        /// </summary>
        UInt64,

        /// <summary>
        /// Four byte floating point scalar.
        /// </summary>
        Float32,
        Float64,

        /// <summary>
        /// Represents an array of characters.
        /// </summary>
        String,

        /// <summary>
        /// Represents an array.
        /// </summary>
        Array,

        /// <summary>
        /// Represents a pointer to data.
        /// </summary>
        Pointer,

        /// <summary>
        /// Represents a user-defined object (such as classes, structs, etc).
        /// </summary>
        UserObject,

        /// <summary>
        /// Represents a vector of any size.
        /// </summary>
        Vector,
    }
}
