using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRExpressionSimplifier : IRPass
    {
        enum BinaryKind
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            And,
            Or,
            Xor,
            ShiftLeft,
            ShiftRight,
        }

        IRModule Module { get; set; }

        IRFunctionAnalysis Analysis { get; set; } = null!;

        bool Changed { get; set; }

        bool InKernel { get; set; }

        public IRExpressionSimplifier(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            this.Changed = false;

            foreach (IRFunction function in this.Module.Functions)
            {
                this.Analysis = new IRFunctionAnalysis(function);
                this.InKernel = function.IsKernel;

                if (this.Analysis.IsSupported)
                {
                    SimplifyBlock(function.Entryblock);
                }
            }

            return this.Changed;
        }

        void SimplifyBlock(IRBlock block)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                SimplifyOperands(instruction);

                foreach (IRBlock nested in IRTree.NestedBlocks(instruction))
                {
                    SimplifyBlock(nested);
                }
            }
        }

        void SimplifyOperands(IRInstruction parent)
        {
            foreach (IRSlot slot in IRTree.Slots(parent))
            {
                IRValueInstruction? value = slot.Value;

                if (value == null || !this.Analysis.IsUnshared(value) || (IRTree.IsInvocation(value) && this.Analysis.Placed.Contains(value)))
                {
                    continue;
                }

                SimplifyOperands(value);

                for (int step = 0; step < 64; step++)
                {
                    IRValueInstruction current = slot.Value!;

                    if (!this.Analysis.IsUnshared(current))
                    {
                        break;
                    }

                    IRValueInstruction? replacement = Simplify(current);

                    if (replacement == null || !Fits(slot, current, replacement))
                    {
                        break;
                    }

                    slot.Replace(replacement);
                    ReleaseReplaced(current, replacement);
                    this.Changed = true;
                }
            }
        }

        static bool Fits(IRSlot slot, IRValueInstruction original, IRValueInstruction replacement)
        {
            if (replacement.ValueType.DataType != original.ValueType.DataType)
            {
                return false;
            }

            if (slot.Kind == IRSlotKind.ArraySize && replacement is IRConstantInteger size)
            {
                return IRNumeric.TryGetInteger(size, size.IntegerType, out long count) && count >= 0 && count <= int.MaxValue
                    && size.Value == count;
            }

            return true;
        }

        void ReleaseReplaced(IRValueInstruction original, IRValueInstruction replacement)
        {
            if (ReferenceEquals(original, replacement))
            {
                return;
            }

            original.References.Clear();
            IRTree.ReleaseOperands(original, this.Analysis, IRTree.Reachable(replacement));
        }

        IRValueInstruction? Simplify(IRValueInstruction value)
        {
            if (this.InKernel && IRTree.Children(value).Append(value).Any(v => IRNumeric.IsFloat(v.ValueType.DataType)))
            {
                return null;
            }

            switch (value)
            {
                case IRCompare compare:
                    return SimplifyCompare(compare);

                case IRConvert convert:
                    return SimplifyConvert(convert);

                case IRLength length:
                    return SimplifyLength(length);
            }

            if (!TryDecompose(value, out BinaryKind kind, out IRValueInstruction left, out IRValueInstruction right))
            {
                return null;
            }

            IRDataTypes type = value.ValueType.DataType;

            if (left.ValueType.DataType != type || right.ValueType.DataType != type || left is IRStructProperty || right is IRStructProperty)
            {
                return null;
            }

            if (IRNumeric.IsInteger(type))
            {
                return SimplifyInteger(kind, left, right, type);
            }

            if (type == IRDataTypes.Bool && kind is BinaryKind.And or BinaryKind.Or or BinaryKind.Xor)
            {
                return SimplifyBool(kind, left, right);
            }

            if (IRNumeric.IsFloat(type) && kind is BinaryKind.Add or BinaryKind.Subtract or BinaryKind.Multiply or BinaryKind.Divide)
            {
                return SimplifyFloat(kind, left, right, type);
            }

            return null;
        }

        static bool TryDecompose(IRValueInstruction value, out BinaryKind kind, out IRValueInstruction left, out IRValueInstruction right)
        {
            (kind, left, right) = value switch
            {
                IRAdd add => (BinaryKind.Add, add.Left, add.Right),
                IRSubtract subtract => (BinaryKind.Subtract, subtract.Left, subtract.Right),
                IRMultiply multiply => (BinaryKind.Multiply, multiply.Left, multiply.Right),
                IRDivide divide => (BinaryKind.Divide, divide.Left, divide.Right),
                IRBitwise bitwise => (bitwise.Operator switch
                {
                    IRBitwiseOperator.And => BinaryKind.And,
                    IRBitwiseOperator.Or => BinaryKind.Or,
                    IRBitwiseOperator.Xor => BinaryKind.Xor,
                    IRBitwiseOperator.ShiftLeft => BinaryKind.ShiftLeft,
                    _ => BinaryKind.ShiftRight,
                }, bitwise.Left, bitwise.Right),
                _ => (default(BinaryKind), null!, null!),
            };

            return left != null;
        }

        static IRValueInstruction Build(BinaryKind kind, IRValueInstruction left, IRValueInstruction right) => kind switch
        {
            BinaryKind.Add => new IRAdd(left, right),
            BinaryKind.Subtract => new IRSubtract(left, right),
            BinaryKind.Multiply => new IRMultiply(left, right),
            BinaryKind.Divide => new IRDivide(left, right),
            BinaryKind.And => new IRBitwise(IRBitwiseOperator.And, left, right),
            BinaryKind.Or => new IRBitwise(IRBitwiseOperator.Or, left, right),
            BinaryKind.Xor => new IRBitwise(IRBitwiseOperator.Xor, left, right),
            BinaryKind.ShiftLeft => new IRBitwise(IRBitwiseOperator.ShiftLeft, left, right),
            _ => new IRBitwise(IRBitwiseOperator.ShiftRight, left, right),
        };

        static bool IsCommutative(BinaryKind kind) => kind is BinaryKind.Add or BinaryKind.Multiply or BinaryKind.And or BinaryKind.Or or BinaryKind.Xor;

        bool IsPure(IRValueInstruction value) => IRTree.IsPure(value, this.Analysis);

        bool SameValue(IRValueInstruction left, IRValueInstruction right) => IsPure(left) && IRTree.AreEquivalent(left, right);

        static long? FoldInteger(BinaryKind kind, long a, long b, IRDataTypes type)
        {
            bool signed = IRNumeric.IsSigned(type);

            switch (kind)
            {
                case BinaryKind.Add: return unchecked(a + b);
                case BinaryKind.Subtract: return unchecked(a - b);
                case BinaryKind.Multiply: return unchecked(a * b);
                case BinaryKind.And: return a & b;
                case BinaryKind.Or: return a | b;
                case BinaryKind.Xor: return a ^ b;
                case BinaryKind.ShiftLeft: return a << (int)(b & IRNumeric.ShiftMask(type));
                case BinaryKind.ShiftRight:
                    {
                        int count = (int)(b & IRNumeric.ShiftMask(type));
                        return signed ? a >> count : (long)(unchecked((ulong)a) >> count);
                    }
                case BinaryKind.Divide:
                    if (b == 0)
                    {
                        return null;
                    }

                    if (signed)
                    {
                        if (b == -1 && IRNumeric.Bits(type) >= 32 && a == IRNumeric.MinimumSigned(type))
                        {
                            return null;
                        }

                        return a / b;
                    }

                    return unchecked((long)(unchecked((ulong)a) / unchecked((ulong)b)));
            }

            return null;
        }

        IRValueInstruction? SimplifyInteger(BinaryKind kind, IRValueInstruction left, IRValueInstruction right, IRDataTypes type)
        {
            bool leftConstant = IRNumeric.TryGetInteger(left, type, out long l);
            bool rightConstant = IRNumeric.TryGetInteger(right, type, out long r);
            long allOnes = IRNumeric.AllOnes(type);
            int mask = IRNumeric.ShiftMask(type);

            if (leftConstant && rightConstant)
            {
                long? folded = FoldInteger(kind, l, r, type);
                return folded == null ? null : IRNumeric.Integer(type, IRNumeric.Normalize(type, folded.Value));
            }

            IRValueInstruction Zero() => IRNumeric.Integer(type, 0)!;

            switch (kind)
            {
                case BinaryKind.Add:
                    if (rightConstant && r == 0) return left;
                    if (leftConstant && l == 0) return right;
                    break;

                case BinaryKind.Subtract:
                    if (rightConstant && r == 0) return left;
                    if (SameValue(left, right)) return Zero();
                    break;

                case BinaryKind.Multiply:
                    if (rightConstant && r == 1) return left;
                    if (leftConstant && l == 1) return right;
                    if (rightConstant && r == 0 && IsPure(left)) return Zero();
                    if (leftConstant && l == 0 && IsPure(right)) return Zero();
                    if (rightConstant && IRNumeric.IsPowerOfTwo(r, type, out int rightShift)) return Build(BinaryKind.ShiftLeft, left, IRNumeric.Integer(type, rightShift)!);
                    if (leftConstant && IRNumeric.IsPowerOfTwo(l, type, out int leftShift)) return Build(BinaryKind.ShiftLeft, right, IRNumeric.Integer(type, leftShift)!);
                    break;

                case BinaryKind.Divide:
                    if (rightConstant && r == 1) return left;
                    if (rightConstant && !IRNumeric.IsSigned(type) && IRNumeric.IsPowerOfTwo(r, type, out int divideShift)) return Build(BinaryKind.ShiftRight, left, IRNumeric.Integer(type, divideShift)!);
                    break;

                case BinaryKind.And:
                    if (rightConstant && r == 0 && IsPure(left)) return Zero();
                    if (leftConstant && l == 0 && IsPure(right)) return Zero();
                    if (rightConstant && r == allOnes) return left;
                    if (leftConstant && l == allOnes) return right;
                    if (SameValue(left, right)) return left;
                    break;

                case BinaryKind.Or:
                    if (rightConstant && r == 0) return left;
                    if (leftConstant && l == 0) return right;
                    if (rightConstant && r == allOnes && IsPure(left)) return IRNumeric.Integer(type, allOnes);
                    if (leftConstant && l == allOnes && IsPure(right)) return IRNumeric.Integer(type, allOnes);
                    if (SameValue(left, right)) return left;
                    break;

                case BinaryKind.Xor:
                    if (rightConstant && r == 0) return left;
                    if (leftConstant && l == 0) return right;
                    if (SameValue(left, right)) return Zero();
                    break;

                case BinaryKind.ShiftLeft:
                case BinaryKind.ShiftRight:
                    if (rightConstant && (r & mask) == 0) return left;
                    if (leftConstant && l == 0 && IsPure(right)) return Zero();
                    break;
            }

            if (rightConstant)
            {
                IRValueInstruction? merged = Reassociate(kind, left, r, type);

                if (merged != null)
                {
                    return merged;
                }
            }

            if (IsCommutative(kind) && leftConstant && right is not IRConstantInteger)
            {
                return Build(kind, right, left);
            }

            return null;
        }

        IRValueInstruction? Reassociate(BinaryKind outer, IRValueInstruction left, long c2, IRDataTypes type)
        {
            if (!this.Analysis.IsUnshared(left) || !TryDecompose(left, out BinaryKind inner, out IRValueInstruction x, out IRValueInstruction innerRight)
                || left.ValueType.DataType != type || x.ValueType.DataType != type || x is IRStructProperty
                || !IRNumeric.TryGetInteger(innerRight, type, out long c1))
            {
                return null;
            }

            int mask = IRNumeric.ShiftMask(type);

            (BinaryKind Kind, long Constant)? merged = (inner, outer) switch
            {
                (BinaryKind.Add, BinaryKind.Add) => (BinaryKind.Add, unchecked(c1 + c2)),
                (BinaryKind.Subtract, BinaryKind.Add) => (BinaryKind.Add, unchecked(c2 - c1)),
                (BinaryKind.Add, BinaryKind.Subtract) => (BinaryKind.Add, unchecked(c1 - c2)),
                (BinaryKind.Subtract, BinaryKind.Subtract) => (BinaryKind.Subtract, unchecked(c1 + c2)),
                (BinaryKind.Multiply, BinaryKind.Multiply) => (BinaryKind.Multiply, unchecked(c1 * c2)),
                (BinaryKind.And, BinaryKind.And) => (BinaryKind.And, c1 & c2),
                (BinaryKind.Or, BinaryKind.Or) => (BinaryKind.Or, c1 | c2),
                (BinaryKind.Xor, BinaryKind.Xor) => (BinaryKind.Xor, c1 ^ c2),
                (BinaryKind.ShiftLeft, BinaryKind.ShiftLeft) or (BinaryKind.ShiftRight, BinaryKind.ShiftRight)
                    when c1 >= 0 && c2 >= 0 && c1 <= mask && c2 <= mask && c1 + c2 <= mask && c1 + c2 < IRNumeric.Bits(type) => (inner, c1 + c2),
                _ => null,
            };

            if (merged == null)
            {
                return null;
            }

            IRConstantInteger? constant = IRNumeric.Integer(type, IRNumeric.Normalize(type, merged.Value.Constant));
            return constant == null ? null : Build(merged.Value.Kind, x, constant);
        }

        IRValueInstruction? SimplifyBool(BinaryKind kind, IRValueInstruction left, IRValueInstruction right)
        {
            bool leftConstant = IRNumeric.TryGetInteger(left, IRDataTypes.Bool, out long l);
            bool rightConstant = IRNumeric.TryGetInteger(right, IRDataTypes.Bool, out long r);

            if (leftConstant && rightConstant)
            {
                return IRNumeric.Integer(IRDataTypes.Bool, kind switch
                {
                    BinaryKind.And => l & r,
                    BinaryKind.Or => l | r,
                    _ => l ^ r,
                });
            }

            switch (kind)
            {
                case BinaryKind.And:
                    if (rightConstant && r == 0 && IsPure(left)) return IRNumeric.Bool(false);
                    if (leftConstant && l == 0 && IsPure(right)) return IRNumeric.Bool(false);
                    if (SameValue(left, right)) return left;
                    break;

                case BinaryKind.Or:
                    if (rightConstant && r == 0) return left;
                    if (leftConstant && l == 0) return right;
                    if (SameValue(left, right)) return left;
                    break;

                case BinaryKind.Xor:
                    if (rightConstant && r == 0) return left;
                    if (leftConstant && l == 0) return right;
                    if (SameValue(left, right)) return IRNumeric.Bool(false);
                    break;
            }

            if (leftConstant && right is not IRConstantInteger)
            {
                return Build(kind, right, left);
            }

            return null;
        }

        IRValueInstruction? SimplifyFloat(BinaryKind kind, IRValueInstruction left, IRValueInstruction right, IRDataTypes type)
        {
            bool leftConstant = IRNumeric.TryGetFloat(left, type, out double l);
            bool rightConstant = IRNumeric.TryGetFloat(right, type, out double r);
            bool single = type == IRDataTypes.Float32;

            if (leftConstant && rightConstant)
            {
                double result = (kind, single) switch
                {
                    (BinaryKind.Add, true) => (float)l + (float)r,
                    (BinaryKind.Subtract, true) => (float)l - (float)r,
                    (BinaryKind.Multiply, true) => (float)l * (float)r,
                    (BinaryKind.Divide, true) => (float)l / (float)r,
                    (BinaryKind.Add, false) => l + r,
                    (BinaryKind.Subtract, false) => l - r,
                    (BinaryKind.Multiply, false) => l * r,
                    _ => l / r,
                };

                return IRNumeric.Float(type, result);
            }

            static bool IsNegativeZero(double value) => value == 0 && double.IsNegative(value);
            static bool IsPositiveZero(double value) => value == 0 && !double.IsNegative(value);

            switch (kind)
            {
                case BinaryKind.Add:
                    if (rightConstant && IsNegativeZero(r)) return left;
                    if (leftConstant && IsNegativeZero(l)) return right;
                    break;

                case BinaryKind.Subtract:
                    if (rightConstant && IsPositiveZero(r)) return left;
                    break;

                case BinaryKind.Multiply:
                    if (rightConstant && r == 1) return left;
                    if (leftConstant && l == 1) return right;
                    break;

                case BinaryKind.Divide:
                    if (rightConstant && r == 1) return left;
                    if (rightConstant && ExactReciprocal(r, single) is double reciprocal) return Build(BinaryKind.Multiply, left, IRNumeric.Float(type, reciprocal)!);
                    break;
            }

            if (IsCommutative(kind) && leftConstant && right is not IRConstantInteger)
            {
                return Build(kind, right, left);
            }

            return null;
        }

        static double? ExactReciprocal(double divisor, bool single)
        {
            if (double.IsNaN(divisor) || double.IsInfinity(divisor) || divisor == 0)
            {
                return null;
            }

            if (single)
            {
                float value = (float)divisor;
                int bits = BitConverter.SingleToInt32Bits(value);

                if ((bits & 0x7FFFFF) != 0 || !float.IsNormal(value))
                {
                    return null;
                }

                float reciprocal = 1f / value;
                return float.IsNormal(reciprocal) && reciprocal * value == 1f ? reciprocal : null;
            }
            else
            {
                long bits = BitConverter.DoubleToInt64Bits(divisor);

                if ((bits & 0xFFFFFFFFFFFFFL) != 0 || !double.IsNormal(divisor))
                {
                    return null;
                }

                double reciprocal = 1.0 / divisor;
                return double.IsNormal(reciprocal) && reciprocal * divisor == 1.0 ? reciprocal : null;
            }
        }

        static IRComparisonOperator Mirror(IRComparisonOperator op) => op switch
        {
            IRComparisonOperator.LessThan => IRComparisonOperator.GreaterThan,
            IRComparisonOperator.GreaterThan => IRComparisonOperator.LessThan,
            IRComparisonOperator.LessThanOrEqual => IRComparisonOperator.GreaterThanOrEqual,
            IRComparisonOperator.GreaterThanOrEqual => IRComparisonOperator.LessThanOrEqual,
            _ => op,
        };

        internal static IRComparisonOperator Invert(IRComparisonOperator op) => op switch
        {
            IRComparisonOperator.EqualTo => IRComparisonOperator.NotEqualTo,
            IRComparisonOperator.NotEqualTo => IRComparisonOperator.EqualTo,
            IRComparisonOperator.LessThan => IRComparisonOperator.GreaterThanOrEqual,
            IRComparisonOperator.GreaterThanOrEqual => IRComparisonOperator.LessThan,
            IRComparisonOperator.GreaterThan => IRComparisonOperator.LessThanOrEqual,
            _ => IRComparisonOperator.GreaterThan,
        };

        static bool Evaluate(IRComparisonOperator op, int order) => op switch
        {
            IRComparisonOperator.EqualTo => order == 0,
            IRComparisonOperator.NotEqualTo => order != 0,
            IRComparisonOperator.LessThan => order < 0,
            IRComparisonOperator.LessThanOrEqual => order <= 0,
            IRComparisonOperator.GreaterThan => order > 0,
            _ => order >= 0,
        };

        static bool EvaluateFloat(IRComparisonOperator op, double a, double b) => op switch
        {
            IRComparisonOperator.EqualTo => a == b,
            IRComparisonOperator.NotEqualTo => a != b,
            IRComparisonOperator.LessThan => a < b,
            IRComparisonOperator.LessThanOrEqual => a <= b,
            IRComparisonOperator.GreaterThan => a > b,
            _ => a >= b,
        };

        internal static bool IsInvertible(IRCompare compare)
            => IRNumeric.IsIntegral(compare.OperandType.DataType)
                && compare.Left.ValueType.DataType == compare.OperandType.DataType
                && compare.Right.ValueType.DataType == compare.OperandType.DataType;

        IRValueInstruction? SimplifyCompare(IRCompare compare)
        {
            IRDataTypes type = compare.OperandType.DataType;
            IRValueInstruction left = compare.Left;
            IRValueInstruction right = compare.Right;

            if (!IRNumeric.IsNumeric(type) || left.ValueType.DataType != type || right.ValueType.DataType != type
                || left is IRStructProperty || right is IRStructProperty)
            {
                return null;
            }

            IRComparisonOperator op = compare.Operator;

            if (IRNumeric.IsFloat(type))
            {
                if (IRNumeric.TryGetFloat(left, type, out double a) && IRNumeric.TryGetFloat(right, type, out double b))
                {
                    return IRNumeric.Bool(EvaluateFloat(op, a, b));
                }

                if (left is IRConstantInteger && right is not IRConstantInteger)
                {
                    return new IRCompare(Mirror(op), right, left);
                }

                return null;
            }

            bool signed = IRNumeric.IsSigned(type);
            bool leftConstant = IRNumeric.TryGetInteger(left, type, out long l);
            bool rightConstant = IRNumeric.TryGetInteger(right, type, out long r);

            if (leftConstant && rightConstant)
            {
                int order = signed ? l.CompareTo(r) : unchecked((ulong)l).CompareTo(unchecked((ulong)r));
                return IRNumeric.Bool(Evaluate(op, order));
            }

            if (SameValue(left, right))
            {
                return IRNumeric.Bool(Evaluate(op, 0));
            }

            if (!signed && rightConstant && r == 0 && IsPure(left))
            {
                if (op == IRComparisonOperator.LessThan) return IRNumeric.Bool(false);
                if (op == IRComparisonOperator.GreaterThanOrEqual) return IRNumeric.Bool(true);
            }

            if (!signed && leftConstant && l == 0 && IsPure(right))
            {
                if (op == IRComparisonOperator.GreaterThan) return IRNumeric.Bool(false);
                if (op == IRComparisonOperator.LessThanOrEqual) return IRNumeric.Bool(true);
            }

            if (type == IRDataTypes.Bool && rightConstant && op is IRComparisonOperator.EqualTo or IRComparisonOperator.NotEqualTo
                && left is IRCompare inner && this.Analysis.IsUnshared(inner) && IsInvertible(inner))
            {
                bool keep = (op == IRComparisonOperator.EqualTo) == (r == 1);

                if (keep)
                {
                    return inner;
                }

                return new IRCompare(Invert(inner.Operator), inner.Left, inner.Right);
            }

            if (leftConstant && right is not IRConstantInteger)
            {
                return new IRCompare(Mirror(op), right, left);
            }

            return null;
        }

        IRValueInstruction? SimplifyConvert(IRConvert convert)
        {
            IRDataTypes source = convert.Value.ValueType.DataType;
            IRDataTypes target = convert.ValueType.DataType;

            if (!IRNumeric.IsNumeric(source) || !IRNumeric.IsNumeric(target) || convert.Value is IRStructProperty)
            {
                return null;
            }

            IRConstantInteger? folded = IRNumeric.Convert(convert.Value, source, target);

            if (folded != null)
            {
                return folded;
            }

            if (source == target && target != IRDataTypes.Bool)
            {
                return convert.Value;
            }

            if (convert.Value is IRConvert inner && this.Analysis.IsUnshared(inner) && inner.Value is not IRStructProperty)
            {
                IRDataTypes original = inner.Value.ValueType.DataType;

                if (IRNumeric.IsNumeric(original) && IRNumeric.Contains(source, original) && !(IRNumeric.IsFloat(source) && !IRNumeric.IsFloat(original) && target == IRDataTypes.UInt64))
                {
                    return new IRConvert(inner.Value, convert.ValueType);
                }
            }

            return null;
        }

        IRValueInstruction? SimplifyLength(IRLength length)
        {
            switch (length.Target)
            {
                case IRConstantString text:
                    return IRNumeric.Integer(IRDataTypes.Int32, System.Text.Encoding.UTF8.GetByteCount(text.Value));

                case IRConstantBytes bytes:
                    return IRNumeric.Integer(IRDataTypes.Int32, bytes.Value.Length);

                case IRFatPointer fatPointer when this.Analysis.IsUnshared(fatPointer) && IsPure(fatPointer.Pointer)
                    && fatPointer.Length.ValueType.DataType == IRDataTypes.Int32 && fatPointer.Length is not IRStructProperty:
                    return fatPointer.Length;
            }

            return null;
        }
    }
}
