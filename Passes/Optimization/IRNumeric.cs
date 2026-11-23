using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    internal static class IRNumeric
    {
        const double TwoPow53 = 9007199254740992.0;

        public static bool IsInteger(IRDataTypes type)
            => type is IRDataTypes.Int8 or IRDataTypes.UInt8 or IRDataTypes.Int16 or IRDataTypes.UInt16
                or IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Int64 or IRDataTypes.UInt64;

        public static bool IsIntegral(IRDataTypes type) => IsInteger(type) || type == IRDataTypes.Bool;

        public static bool IsFloat(IRDataTypes type) => type is IRDataTypes.Float32 or IRDataTypes.Float64;

        public static bool IsNumeric(IRDataTypes type) => IsIntegral(type) || IsFloat(type);

        public static bool IsSigned(IRDataTypes type)
            => type is IRDataTypes.Int8 or IRDataTypes.Int16 or IRDataTypes.Int32 or IRDataTypes.Int64;

        public static int Bits(IRDataTypes type) => type switch
        {
            IRDataTypes.Bool or IRDataTypes.Int8 or IRDataTypes.UInt8 => 8,
            IRDataTypes.Int16 or IRDataTypes.UInt16 => 16,
            IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Float32 => 32,
            _ => 64,
        };

        public static int ShiftMask(IRDataTypes type) => Bits(type) == 64 ? 63 : 31;

        public static long Normalize(IRDataTypes type, long value) => type switch
        {
            IRDataTypes.Bool or IRDataTypes.UInt8 => (byte)value,
            IRDataTypes.Int8 => (sbyte)value,
            IRDataTypes.Int16 => (short)value,
            IRDataTypes.UInt16 => (ushort)value,
            IRDataTypes.Int32 => (int)value,
            IRDataTypes.UInt32 => (uint)value,
            _ => value,
        };

        public static long AllOnes(IRDataTypes type) => Normalize(type, -1);

        public static long MinimumSigned(IRDataTypes type) => type switch
        {
            IRDataTypes.Int8 => sbyte.MinValue,
            IRDataTypes.Int16 => short.MinValue,
            IRDataTypes.Int32 => int.MinValue,
            _ => long.MinValue,
        };

        static long EncodeConstant(IRDataTypes type, double value)
        {
            if (type is IRDataTypes.UInt64 && value >= 9223372036854775808.0)
            {
                return unchecked((long)(ulong)value);
            }

            long integer = (long)value;

            return type switch
            {
                IRDataTypes.Bool => integer != 0 ? 1 : 0,
                _ => Normalize(type, integer),
            };
        }

        public static bool TryGetInteger(IRValueInstruction value, IRDataTypes type, out long bits)
        {
            bits = 0;

            if (value is not IRConstantInteger constant || constant.IntegerType != type || !IsIntegral(type))
            {
                return false;
            }

            if (!double.IsFinite(constant.Value) || constant.Value <= -9223372036854775808.0 || constant.Value >= 18446744073709551616.0)
            {
                return false;
            }

            if (constant.Value >= 9223372036854775808.0 && type != IRDataTypes.UInt64)
            {
                return false;
            }

            bits = EncodeConstant(type, constant.Value);
            return true;
        }

        public static bool TryGetFloat(IRValueInstruction value, IRDataTypes type, out double result)
        {
            result = 0;

            if (value is not IRConstantInteger constant || constant.IntegerType != type || !IsFloat(type))
            {
                return false;
            }

            result = type == IRDataTypes.Float32 ? (float)constant.Value : constant.Value;
            return true;
        }

        public static IRConstantInteger? Integer(IRDataTypes type, long bits)
        {
            if (!IsIntegral(type) || Normalize(type, bits) != bits || (type == IRDataTypes.Bool && bits is not (0 or 1)))
            {
                return null;
            }

            double value = type == IRDataTypes.UInt64 ? (double)(ulong)bits : bits;

            if (value >= 18446744073709551616.0 || EncodeConstant(type, value) != bits)
            {
                return null;
            }

            return new IRConstantInteger(type, value);
        }

        public static IRConstantInteger? Float(IRDataTypes type, double value)
        {
            if (!IsFloat(type) || double.IsNaN(value))
            {
                return null;
            }

            return new IRConstantInteger(type, type == IRDataTypes.Float32 ? (double)(float)value : value);
        }

        public static IRConstantInteger Bool(bool value) => new IRConstantInteger(IRDataTypes.Bool, value ? 1 : 0);

        public static bool IsPowerOfTwo(long bits, IRDataTypes type, out int exponent)
        {
            exponent = 0;
            int limit = IsSigned(type) ? Bits(type) - 2 : Bits(type) - 1;
            ulong value = unchecked((ulong)bits);

            if (bits <= 0 && !(type == IRDataTypes.UInt64 && bits != 0))
            {
                return false;
            }

            if ((value & (value - 1)) != 0)
            {
                return false;
            }

            exponent = System.Numerics.BitOperations.TrailingZeroCount(value);
            return exponent >= 1 && exponent <= limit;
        }

        public static bool Contains(IRDataTypes outer, IRDataTypes inner)
        {
            if (outer == inner)
            {
                return true;
            }

            if (IsIntegral(outer) && IsIntegral(inner))
            {
                if (outer == IRDataTypes.Bool)
                {
                    return false;
                }

                (decimal innerMin, decimal innerMax) = Range(inner);
                (decimal outerMin, decimal outerMax) = Range(outer);
                return outerMin <= innerMin && innerMax <= outerMax;
            }

            if (IsFloat(outer) && IsIntegral(inner))
            {
                return outer == IRDataTypes.Float64 ? Bits(inner) <= 32 : Bits(inner) <= 16;
            }

            return outer == IRDataTypes.Float64 && inner == IRDataTypes.Float32;
        }

        static (decimal Min, decimal Max) Range(IRDataTypes type) => type switch
        {
            IRDataTypes.Bool => (0, byte.MaxValue),
            IRDataTypes.Int8 => (sbyte.MinValue, sbyte.MaxValue),
            IRDataTypes.UInt8 => (byte.MinValue, byte.MaxValue),
            IRDataTypes.Int16 => (short.MinValue, short.MaxValue),
            IRDataTypes.UInt16 => (ushort.MinValue, ushort.MaxValue),
            IRDataTypes.Int32 => (int.MinValue, int.MaxValue),
            IRDataTypes.UInt32 => (uint.MinValue, uint.MaxValue),
            IRDataTypes.Int64 => (long.MinValue, long.MaxValue),
            _ => (ulong.MinValue, ulong.MaxValue),
        };

        public static IRConstantInteger? Convert(IRValueInstruction value, IRDataTypes source, IRDataTypes target)
        {
            if (IsIntegral(source) && TryGetInteger(value, source, out long bits))
            {
                if (target == IRDataTypes.Bool)
                {
                    return Bool(bits != 0);
                }

                if (IsInteger(target))
                {
                    return Integer(target, Normalize(target, bits));
                }

                if (IsFloat(target))
                {
                    double exact = source == IRDataTypes.UInt64 ? (double)(ulong)bits : bits;

                    if (Math.Abs(exact) > TwoPow53)
                    {
                        return null;
                    }

                    return Float(target, target == IRDataTypes.Float32 ? (float)exact : exact);
                }

                return null;
            }

            if (IsFloat(source) && TryGetFloat(value, source, out double number))
            {
                if (target == IRDataTypes.Bool)
                {
                    return Bool(number != 0 || double.IsNaN(number));
                }

                if (IsFloat(target))
                {
                    return Float(target, number);
                }

                if (!IsInteger(target) || double.IsNaN(number))
                {
                    return null;
                }

                double truncated = Math.Truncate(number);

                if (Math.Abs(truncated) > TwoPow53 || (target == IRDataTypes.UInt64 && truncated < 0))
                {
                    return null;
                }

                return Integer(target, Normalize(target, (long)truncated));
            }

            return null;
        }
    }
}
