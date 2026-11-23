using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;

namespace CommonIR.Generators.Binary.Xtensa.Emission
{
    internal enum XtensaValueClass
    {
        Void,
        Word,
        Pair,
        Single,
        Double,
        Fat,
    }

    internal static class XtensaLayout
    {
        public static XtensaValueClass Classify(IRType type) => Classify(type.DataType);

        public static XtensaValueClass Classify(IRDataTypes dataType) => dataType switch
        {
            IRDataTypes.Void => XtensaValueClass.Void,
            IRDataTypes.Float32 => XtensaValueClass.Single,
            IRDataTypes.Float64 => XtensaValueClass.Double,
            IRDataTypes.Int64 or IRDataTypes.UInt64 => XtensaValueClass.Pair,
            IRDataTypes.String or IRDataTypes.Array or IRDataTypes.FatPointer => XtensaValueClass.Fat,

            IRDataTypes.Bool
            or IRDataTypes.Int8 or IRDataTypes.UInt8
            or IRDataTypes.Int16 or IRDataTypes.UInt16
            or IRDataTypes.Int32 or IRDataTypes.UInt32
            or IRDataTypes.Pointer or IRDataTypes.Struct => XtensaValueClass.Word,

            _ => throw ErrorHandler.CreateNotImplimented($"Values of type '{dataType}' are not supported when targeting Xtensa.")
        };

        public static int Words(XtensaValueClass valueClass) => valueClass switch
        {
            XtensaValueClass.Void => 0,
            XtensaValueClass.Pair or XtensaValueClass.Double or XtensaValueClass.Fat => 2,
            _ => 1,
        };

        public static int Words(IRType type) => Words(Classify(type));

        public static int SizeOf(IRType type) => SizeOf(type.DataType);

        public static int SizeOf(IRDataTypes dataType) => dataType switch
        {
            IRDataTypes.Bool or IRDataTypes.Int8 or IRDataTypes.UInt8 => 1,
            IRDataTypes.Int16 or IRDataTypes.UInt16 => 2,
            IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Float32 or IRDataTypes.Pointer or IRDataTypes.Struct => 4,
            IRDataTypes.Int64 or IRDataTypes.UInt64 or IRDataTypes.Float64 => 8,
            IRDataTypes.String or IRDataTypes.Array or IRDataTypes.FatPointer => 8,
            _ => throw ErrorHandler.CreateNotImplimented($"Values of type '{dataType}' have no size when targeting Xtensa.")
        };

        public static int ElementSize(IRType type) => IRStructSchema.IsInline(type) ? SizeOf((IRStructSchema)type.UserObject!) : SizeOf(type);

        public static int AlignmentOf(IRType type) => Math.Min(SizeOf(type), 4);

        public static bool IsSigned(IRType type) => IsSigned(type.DataType);

        public static bool IsSigned(IRDataTypes dataType) => dataType is IRDataTypes.Int8 or IRDataTypes.Int16 or IRDataTypes.Int32 or IRDataTypes.Int64;

        public static int AlignUp(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

        public static int OffsetOf(IRStructProperty property)
        {
            if (property.Parent is not IRStructSchema schema)
            {
                throw ErrorHandler.Create($"Struct property '{property.Name}' does not belong to a struct.");
            }

            int offset = 0;

            foreach (IRStructProperty current in schema.Properties)
            {
                offset = AlignUp(offset, AlignmentOf(current.ValueType));

                if (current == property)
                {
                    return offset;
                }

                offset += SizeOf(current.ValueType);
            }

            throw ErrorHandler.Create($"Struct property '{property.Name}' was not found in '{schema.Name}'.");
        }

        public static int SizeOf(IRStructSchema schema)
        {
            int offset = 0;

            foreach (IRStructProperty property in schema.Properties)
            {
                offset = AlignUp(offset, AlignmentOf(property.ValueType));
                offset += SizeOf(property.ValueType);
            }

            return Math.Max(4, AlignUp(offset, 4));
        }

        public static long Bits(IRDataTypes dataType, double value)
        {
            switch (dataType)
            {
                case IRDataTypes.Float32:
                    return (uint)BitConverter.SingleToInt32Bits((float)value);

                case IRDataTypes.Float64:
                    return BitConverter.DoubleToInt64Bits(value);

                case IRDataTypes.UInt64 when value >= 9223372036854775808.0:
                    return unchecked((long)(ulong)value);
            }

            long integer = (long)value;

            return dataType switch
            {
                IRDataTypes.Bool => integer != 0 ? 1 : 0,
                IRDataTypes.Int8 => (sbyte)integer,
                IRDataTypes.UInt8 => (byte)integer,
                IRDataTypes.Int16 => (short)integer,
                IRDataTypes.UInt16 => (ushort)integer,
                IRDataTypes.Int32 => (int)integer,
                IRDataTypes.UInt32 or IRDataTypes.Pointer or IRDataTypes.Struct => (uint)integer,
                _ => integer,
            };
        }
    }
}
