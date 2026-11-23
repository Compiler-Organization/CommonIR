using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Emission
{
    internal enum X8664ValueClass
    {
        Void,
        Integer,
        Float32,
        Float64,
        Fat,
    }

    internal static class X8664Layout
    {
        public static X8664ValueClass Classify(IRType type) => Classify(type.DataType);

        public static X8664ValueClass Classify(IRDataTypes dataType) => dataType switch
        {
            IRDataTypes.Void => X8664ValueClass.Void,
            IRDataTypes.Float32 => X8664ValueClass.Float32,
            IRDataTypes.Float64 => X8664ValueClass.Float64,
            IRDataTypes.String or IRDataTypes.Array or IRDataTypes.FatPointer => X8664ValueClass.Fat,

            IRDataTypes.Bool
            or IRDataTypes.Int8 or IRDataTypes.UInt8
            or IRDataTypes.Int16 or IRDataTypes.UInt16
            or IRDataTypes.Int32 or IRDataTypes.UInt32
            or IRDataTypes.Int64 or IRDataTypes.UInt64
            or IRDataTypes.Pointer or IRDataTypes.Struct => X8664ValueClass.Integer,

            _ => throw ErrorHandler.CreateNotImplimented($"Values of type '{dataType}' are not supported when targeting x86-64.")
        };

        public static int SizeOf(IRType type) => SizeOf(type.DataType);

        public static int SizeOf(IRDataTypes dataType) => dataType switch
        {
            IRDataTypes.Bool or IRDataTypes.Int8 or IRDataTypes.UInt8 => 1,
            IRDataTypes.Int16 or IRDataTypes.UInt16 => 2,
            IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Float32 => 4,
            IRDataTypes.Int64 or IRDataTypes.UInt64 or IRDataTypes.Float64 or IRDataTypes.Pointer or IRDataTypes.Struct => 8,
            IRDataTypes.String or IRDataTypes.Array or IRDataTypes.FatPointer => 16,
            _ => throw ErrorHandler.CreateNotImplimented($"Values of type '{dataType}' have no size when targeting x86-64.")
        };

        public static int ElementSize(IRType type) => IRStructSchema.IsInline(type) ? SizeOf((IRStructSchema)type.UserObject!) : SizeOf(type);

        public static int AlignmentOf(IRType type) => Math.Min(SizeOf(type), 8);

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

            return Math.Max(8, AlignUp(offset, 8));
        }

        public static long NormalizeConstant(IRDataTypes dataType, double value)
        {
            if (dataType is IRDataTypes.UInt64 && value >= 9223372036854775808.0)
            {
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
                IRDataTypes.UInt32 => (uint)integer,
                _ => integer,
            };
        }
    }
}
