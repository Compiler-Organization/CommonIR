using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Runtime;
using CommonIR.Generators.Binary.Xtensa.Emission;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CommonIR.Generators.Binary.Xtensa.Runtime
{
    internal class XtensaRuntimeBuilder
    {
        public const string DeviceModule = "device";

        const uint SerialData = 0x60038000;
        const uint SerialConfig = 0x60038004;
        const uint TimerOperation = 0x60023004;
        const uint TimerHigh = 0x60023040;
        const uint TimerLow = 0x60023044;
        const uint RandomData = 0x6003507C;
        const int TimerTicksPerMillisecond = 16000;
        const int SmallestClass = 4;
        const int LargestClass = 12;
        const int SerialSpin = 40000;

        public const byte ExitMarker = 4;

        IRModule Module { get; }

        CommonIRXtensaConfiguration Configuration { get; }

        Dictionary<string, IRFunctionImport> Imports { get; } = new Dictionary<string, IRFunctionImport>();

        public IRGlobal HeapNext { get; private set; } = null!;

        public IRGlobal HeapStart { get; private set; } = null!;

        public IRGlobal HeapEnd { get; private set; } = null!;

        IRGlobal Bins { get; set; } = null!;

        IRGlobal Large { get; set; } = null!;

        IRGlobal SerialDropped { get; set; } = null!;

        IRGlobal TimeStart { get; set; } = null!;

        IRGlobal? LiveAllocations { get; set; }

        public IRFunction Panic { get; private set; } = null!;

        public IRFunction Malloc { get; private set; } = null!;

        public IRFunction Free { get; private set; } = null!;

        public IRFunction Start { get; private set; } = null!;

        public IRFunction Exception { get; private set; } = null!;

        IRFunction SerialWrite { get; set; } = null!;

        IRFunction SerialNumber { get; set; } = null!;

        IRFunction Halt { get; set; } = null!;

        IRFunction Ticks { get; set; } = null!;

        IRFunction Log { get; set; } = null!;

        IRFunction Write { get; set; } = null!;

        IRFunction Millis { get; set; } = null!;

        IRFunction Sleep { get; set; } = null!;

        IRFunction Entropy { get; set; } = null!;

        IRFunction UnsignedDivide { get; set; } = null!;

        IRFunction UnsignedRemainder { get; set; } = null!;

        IRFunction SignedDivide { get; set; } = null!;

        IRFunction SignedRemainder { get; set; } = null!;

        IRFunctionImport Read32Import { get; set; } = null!;

        IRFunctionImport Write32Import { get; set; } = null!;

        public XtensaRuntimeBuilder(IRModule module, CommonIRXtensaConfiguration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        X8664RuntimeWriter Writer(IRFunction function) => new X8664RuntimeWriter(this.Module, function, this.Imports);

        IRGlobal Global(string name, IRDataTypes dataType) => this.Module.CreateGlobal(name, new IRType(dataType), new IRConstantInteger(dataType, 0), isMutable: true);

        IRFunction Function(string name, IRDataTypes returnType, params (string Name, IRDataTypes Type)[] parameters)
            => this.Module.CreateFunction(name, [new IRType(returnType)], parameters.Select(p => new IRLocal(p.Name, p.Type, isMutable: false)).ToList(), isExport: false);

        IRValueInstruction Read32(X8664RuntimeWriter w, uint address) => w.Call(this.Read32Import, w.UInt32(address));

        void Write32(X8664RuntimeWriter w, uint address, IRValueInstruction value) => w.Invoke(this.Write32Import, w.UInt32(address), value);

        static IRValueInstruction And(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Builder.BuildBitwise(IRBitwiseOperator.And, left, right);

        static IRValueInstruction Or(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Builder.BuildBitwise(IRBitwiseOperator.Or, left, right);

        static IRValueInstruction ShiftLeft(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Builder.BuildBitwise(IRBitwiseOperator.ShiftLeft, left, right);

        static IRValueInstruction ShiftRight(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Builder.BuildBitwise(IRBitwiseOperator.ShiftRight, left, right);

        static IRValueInstruction Equal(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Compare(IRComparisonOperator.EqualTo, left, right);

        static IRValueInstruction NotEqual(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Compare(IRComparisonOperator.NotEqualTo, left, right);

        static IRValueInstruction Less(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Compare(IRComparisonOperator.LessThan, left, right);

        static IRValueInstruction Greater(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Compare(IRComparisonOperator.GreaterThan, left, right);

        static IRValueInstruction GreaterOrEqual(X8664RuntimeWriter w, IRValueInstruction left, IRValueInstruction right) => w.Compare(IRComparisonOperator.GreaterThanOrEqual, left, right);

        public void Build(XtensaContext context)
        {
            IRFunction entryPoint = this.Module.EntryPoint ?? throw ErrorHandler.Create("An entry point is required when targeting the ESP32.");
            List<IRGlobal> globals = this.Module.Globals.ToList();
            List<IRFunctionImport> programImports = this.Module.FunctionImports.ToList();

            this.Read32Import = this.Module.CreateFunctionImport(DeviceModule, "read32", new IRType(IRDataTypes.UInt32), [new IRLocal("address", IRDataTypes.UInt32, isMutable: false)]);
            this.Write32Import = this.Module.CreateFunctionImport(DeviceModule, "write32", new IRType(IRDataTypes.Void), [new IRLocal("address", IRDataTypes.UInt32, isMutable: false), new IRLocal("value", IRDataTypes.UInt32, isMutable: false)]);

            this.HeapNext = Global("__heap_next", IRDataTypes.Pointer);
            this.HeapStart = Global("__heap_start", IRDataTypes.Pointer);
            this.HeapEnd = Global("__heap_end", IRDataTypes.Pointer);
            this.Bins = Global("__heap_bins", IRDataTypes.Pointer);
            this.Large = Global("__heap_large", IRDataTypes.Pointer);
            this.SerialDropped = Global("__serial_dropped", IRDataTypes.Int32);
            this.TimeStart = Global("__time_start", IRDataTypes.UInt64);
            this.LiveAllocations = this.Module.Globals.FirstOrDefault(g => g.Name == IRFree.LiveAllocationsGlobal);

            BuildSerial();
            BuildHalt();
            BuildPanic();
            BuildMalloc();
            BuildFree();
            BuildDivision();
            BuildClock();
            BuildLog();
            BuildException();

            context.Malloc = this.Malloc;
            context.Free = this.Free;
            context.Panic = this.Panic;
            context.Divide64 = this.SignedDivide;
            context.DivideUnsigned64 = this.UnsignedDivide;
            context.Remainder64 = this.SignedRemainder;
            context.RemainderUnsigned64 = this.UnsignedRemainder;

            XtensaSoftFloat? soft = XtensaSoftFloat.Find(this.Module);
            context.SoftFloat = soft;

            foreach (IRFunctionImport functionImport in this.Module.FunctionImports)
            {
                if (functionImport.ModuleName == DeviceModule)
                {
                    context.Intrinsics[functionImport] = functionImport.Name switch
                    {
                        "read32" => XtensaIntrinsic.Read32,
                        "write32" => XtensaIntrinsic.Write32,
                        "read8" => XtensaIntrinsic.Read8,
                        "write8" => XtensaIntrinsic.Write8,
                        "cycles" => XtensaIntrinsic.Cycles,
                        "address" => XtensaIntrinsic.Address,
                        "barrier" => XtensaIntrinsic.Barrier,
                        _ => throw ErrorHandler.Create($"The ESP32 has no device function '{functionImport.Name}'."),
                    };
                    continue;
                }

                if (functionImport.ModuleName is not ("system" or "console"))
                {
                    continue;
                }

                IRFunction? runtime = functionImport.Name switch
                {
                    "log" or "error" => this.Log,
                    "write" => this.Write,
                    "sleep" => this.Sleep,
                    "millis" => this.Millis,
                    "entropy" => this.Entropy,
                    _ => soft?.Library(functionImport.Name),
                };

                if (runtime != null)
                {
                    context.RuntimeImports[functionImport] = runtime;
                }
            }

            BuildStart(entryPoint, globals);
        }

        void BuildSerial()
        {
            this.SerialWrite = Function("__serial_write", IRDataTypes.Void, ("text", IRDataTypes.String));
            X8664RuntimeWriter w = Writer(this.SerialWrite);

            IRLocal text = w.Parameter(0);
            IRValueInstruction Free() => NotEqual(w, And(w, Read32(w, SerialConfig), w.UInt32(2)), w.UInt32(0));

            w.If(NotEqual(w, this.SerialDropped, w.Int32(0)), () =>
            {
                w.If(Free(), () => w.Set(this.SerialDropped, w.Int32(0)), () => w.Return());
            });

            IRLocal data = w.Local("data", new IRType(IRDataTypes.Pointer), w.DataPointer(text));
            IRLocal length = w.Local("length", new IRType(IRDataTypes.Int32), w.Length(text));
            IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

            w.While(Less(w, i, length), () =>
            {
                IRLocal spin = w.Local("spin", new IRType(IRDataTypes.Int32), w.Int32(0));

                w.While(w.Bool(true), () =>
                {
                    w.If(Free(), () => w.Builder.BuildBreak());
                    w.Set(spin, w.Add(spin, w.Int32(1)));

                    w.If(Greater(w, spin, w.Int32(SerialSpin)), () =>
                    {
                        w.Set(this.SerialDropped, w.Int32(1));
                        w.Return();
                    });
                });

                Write32(w, SerialData, w.Convert(w.ReadAt(data, i, IRDataTypes.UInt8), IRDataTypes.UInt32));
                w.Set(i, w.Add(i, w.Int32(1)));

                w.If(Equal(w, Free(), w.Bool(false)), () => Write32(w, SerialConfig, w.UInt32(1)));
            });

            Write32(w, SerialConfig, w.UInt32(1));
            w.Return();

            this.SerialNumber = Function("__serial_number", IRDataTypes.Void, ("value", IRDataTypes.UInt32), ("radix", IRDataTypes.UInt32));
            w = Writer(this.SerialNumber);

            IRLocal value = w.Local("value", new IRType(IRDataTypes.UInt32), w.Parameter(0));
            IRLocal radix = w.Parameter(1);
            IRLocal divisor = w.Local("divisor", new IRType(IRDataTypes.UInt32), w.UInt32(1));
            IRLocal digits = w.Local("digits", new IRType(IRDataTypes.Pointer), w.DataPointer(w.Builder.BuildConstantString("0123456789ABCDEF")));

            w.While(GreaterOrEqual(w, w.Divide(value, divisor), radix), () => w.Set(divisor, w.Multiply(divisor, radix)));

            w.While(NotEqual(w, divisor, w.UInt32(0)), () =>
            {
                IRLocal digit = w.Local("digit", new IRType(IRDataTypes.UInt32), w.Divide(value, divisor));
                w.Invoke(this.SerialWrite, w.FatPointer(w.Add(digits, digit), w.Int32(1), new IRType(IRDataTypes.String)));
                w.Set(value, w.Subtract(value, w.Multiply(digit, divisor)));
                w.Set(divisor, w.Divide(divisor, radix));
            });

            w.Return();
        }

        void Text(X8664RuntimeWriter w, string text) => w.Invoke(this.SerialWrite, w.Builder.BuildConstantString(text));

        void BuildHalt()
        {
            this.Halt = Function("__halt", IRDataTypes.Void, ("code", IRDataTypes.Int32));
            X8664RuntimeWriter w = Writer(this.Halt);

            Text(w, $"{(char)ExitMarker}exit ");
            w.Invoke(this.SerialNumber, w.Convert(w.Parameter(0), IRDataTypes.UInt32), w.UInt32(10));
            Text(w, "\r\n");

            w.While(w.Bool(true), () => { });
            w.Return();
        }

        void BuildPanic()
        {
            this.Panic = Function("__panic", IRDataTypes.Void, ("message", IRDataTypes.String));
            X8664RuntimeWriter w = Writer(this.Panic);

            w.Invoke(this.SerialWrite, w.Parameter(0));
            Text(w, "\r\n");
            w.Invoke(this.Halt, w.Int32(1));
            w.Return();
        }

        void BuildMalloc()
        {
            IRFunction bump = Function("__heap_bump", IRDataTypes.Pointer, ("size", IRDataTypes.UInt32));
            X8664RuntimeWriter b = Writer(bump);

            IRLocal block = b.Local("block", new IRType(IRDataTypes.Pointer), this.HeapNext);
            b.If(Greater(b, b.Add(block, b.Parameter(0)), this.HeapEnd), () => b.Invoke(this.Panic, b.Builder.BuildConstantString("Out of memory.")));
            b.Set(this.HeapNext, b.Add(block, b.Parameter(0)));
            b.Return(block);

            this.Malloc = Function("__malloc", IRDataTypes.Pointer, ("bytes", IRDataTypes.Int32));
            X8664RuntimeWriter w = Writer(this.Malloc);

            IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt32), w.Convert(w.Parameter(0), IRDataTypes.UInt32));
            IRLocal total = w.Local("total", new IRType(IRDataTypes.UInt32), w.Add(bytes, w.UInt32(8 + 3)));
            w.Set(total, And(w, total, w.UInt32(~3u)));

            IRLocal result = w.Local("result", new IRType(IRDataTypes.Pointer), w.Pointer(0));

            w.If(Greater(w, total, w.UInt32(1u << LargestClass)), () =>
            {
                IRLocal size = w.Local("size", new IRType(IRDataTypes.UInt32), And(w, w.Add(total, w.UInt32(15)), w.UInt32(~15u)));
                IRLocal previous = w.Local("previous", new IRType(IRDataTypes.Pointer), w.Pointer(0));
                IRLocal current = w.Local("current", new IRType(IRDataTypes.Pointer), this.Large);

                w.While(NotEqual(w, current, w.Pointer(0)), () =>
                {
                    w.If(GreaterOrEqual(w, w.Read(current, 0, IRDataTypes.UInt32), size), () =>
                    {
                        IRValueInstruction next = w.Read(current, 4, IRDataTypes.Pointer);
                        w.If(Equal(w, previous, w.Pointer(0)), () => w.Set(this.Large, next), () => w.Write(previous, 4, w.Read(current, 4, IRDataTypes.Pointer)));
                        w.Set(result, current);
                        w.Builder.BuildBreak();
                    });

                    w.Set(previous, current);
                    w.Set(current, w.Read(current, 4, IRDataTypes.Pointer));
                });

                w.If(Equal(w, result, w.Pointer(0)), () =>
                {
                    w.Set(result, w.Call(bump, size));
                    w.Write(result, 0, size);
                });
            }, () =>
            {
                IRLocal index = w.Local("index", new IRType(IRDataTypes.UInt32), w.UInt32(SmallestClass));

                w.While(Less(w, ShiftLeft(w, w.UInt32(1), index), total), () => w.Set(index, w.Add(index, w.UInt32(1))));

                IRLocal bin = w.Local("bin", new IRType(IRDataTypes.Pointer), w.Add(this.Bins, w.Multiply(index, w.UInt32(4))));
                w.Set(result, w.Read(bin, 0, IRDataTypes.Pointer));

                w.If(Equal(w, result, w.Pointer(0)), () =>
                {
                    w.Set(result, w.Call(bump, ShiftLeft(w, w.UInt32(1), index)));
                }, () =>
                {
                    w.Write(bin, 0, w.Read(result, 4, IRDataTypes.Pointer));
                });

                w.Write(result, 0, index);
            });

            IRLocal end = w.Local("end", new IRType(IRDataTypes.Pointer), w.Add(result, total));
            IRLocal cursor = w.Local("cursor", new IRType(IRDataTypes.Pointer), w.Add(result, w.UInt32(4)));

            w.While(Less(w, cursor, end), () =>
            {
                w.Write(cursor, 0, w.UInt32(0));
                w.Set(cursor, w.Add(cursor, w.UInt32(4)));
            });

            if (this.LiveAllocations != null)
            {
                w.Set(this.LiveAllocations, w.Add(this.LiveAllocations, w.Int64(1)));
            }

            w.Return(w.Add(result, w.UInt32(8)));
        }

        void BuildFree()
        {
            this.Free = Function("__free", IRDataTypes.Void, ("pointer", IRDataTypes.Pointer));
            X8664RuntimeWriter w = Writer(this.Free);

            IRLocal pointer = w.Parameter(0);

            w.If(Less(w, pointer, w.Add(this.HeapStart, w.UInt32(8))), () => w.Return());
            w.If(GreaterOrEqual(w, pointer, this.HeapEnd), () => w.Return());

            IRLocal block = w.Local("block", new IRType(IRDataTypes.Pointer), w.Subtract(pointer, w.UInt32(8)));
            IRLocal tag = w.Local("tag", new IRType(IRDataTypes.UInt32), w.Read(block, 0, IRDataTypes.UInt32));

            w.If(Greater(w, tag, w.UInt32(LargestClass)), () =>
            {
                w.Write(block, 4, this.Large);
                w.Set(this.Large, block);
            }, () =>
            {
                IRLocal bin = w.Local("bin", new IRType(IRDataTypes.Pointer), w.Add(this.Bins, w.Multiply(tag, w.UInt32(4))));
                w.Write(block, 4, w.Read(bin, 0, IRDataTypes.Pointer));
                w.Write(bin, 0, block);
            });

            if (this.LiveAllocations != null)
            {
                w.Set(this.LiveAllocations, w.Subtract(this.LiveAllocations, w.Int64(1)));
            }

            w.Return();
        }

        void BuildDivision()
        {
            IRFunction Unsigned(string name, bool remainder)
            {
                IRFunction function = Function(name, IRDataTypes.UInt64, ("n", IRDataTypes.UInt64), ("d", IRDataTypes.UInt64));
                X8664RuntimeWriter w = Writer(function);

                IRLocal n = w.Parameter(0);
                IRLocal d = w.Parameter(1);

                w.If(Equal(w, d, w.UInt64(0)), () => w.Invoke(this.Panic, w.Builder.BuildConstantString(XtensaFunctionEmitter.DivideByZeroMessage)));

                w.If(Equal(w, ShiftRight(w, Or(w, n, d), w.UInt64(32)), w.UInt64(0)), () =>
                {
                    IRValueInstruction small = w.Convert(n, IRDataTypes.UInt32);
                    IRValueInstruction divisor = w.Convert(d, IRDataTypes.UInt32);
                    IRValueInstruction quotient = w.Divide(small, divisor);

                    w.Return(w.Convert(remainder ? w.Subtract(w.Convert(n, IRDataTypes.UInt32), w.Multiply(w.Divide(w.Convert(n, IRDataTypes.UInt32), w.Convert(d, IRDataTypes.UInt32)), w.Convert(d, IRDataTypes.UInt32))) : quotient, IRDataTypes.UInt64));
                });

                IRLocal q = w.Local("q", new IRType(IRDataTypes.UInt64), w.UInt64(0));
                IRLocal r = w.Local("r", new IRType(IRDataTypes.UInt64), w.UInt64(0));
                IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(63));

                w.While(GreaterOrEqual(w, i, w.Int32(0)), () =>
                {
                    w.Set(r, Or(w, ShiftLeft(w, r, w.UInt64(1)), And(w, ShiftRight(w, n, w.Convert(i, IRDataTypes.UInt64)), w.UInt64(1))));

                    w.If(GreaterOrEqual(w, r, d), () =>
                    {
                        w.Set(r, w.Subtract(r, d));
                        w.Set(q, Or(w, q, ShiftLeft(w, w.UInt64(1), w.Convert(i, IRDataTypes.UInt64))));
                    });

                    w.Set(i, w.Subtract(i, w.Int32(1)));
                });

                w.Return(remainder ? r : q);
                return function;
            }

            this.UnsignedDivide = Unsigned("__udiv64", false);
            this.UnsignedRemainder = Unsigned("__urem64", true);

            IRFunction Signed(string name, bool remainder)
            {
                IRFunction function = Function(name, IRDataTypes.Int64, ("a", IRDataTypes.Int64), ("b", IRDataTypes.Int64));
                X8664RuntimeWriter w = Writer(function);

                IRLocal a = w.Parameter(0);
                IRLocal b = w.Parameter(1);
                IRLocal ua = w.Local("ua", new IRType(IRDataTypes.UInt64), w.Convert(a, IRDataTypes.UInt64));
                IRLocal ub = w.Local("ub", new IRType(IRDataTypes.UInt64), w.Convert(b, IRDataTypes.UInt64));

                w.If(Less(w, a, w.Int64(0)), () => w.Set(ua, w.Subtract(w.UInt64(0), ua)));
                w.If(Less(w, b, w.Int64(0)), () => w.Set(ub, w.Subtract(w.UInt64(0), ub)));

                IRLocal result = w.Local("result", new IRType(IRDataTypes.UInt64), w.Call(remainder ? this.UnsignedRemainder : this.UnsignedDivide, ua, ub));
                IRValueInstruction negative = remainder
                    ? Less(w, a, w.Int64(0))
                    : NotEqual(w, Less(w, a, w.Int64(0)), Less(w, b, w.Int64(0)));

                w.If(negative, () => w.Set(result, w.Subtract(w.UInt64(0), result)));
                w.Return(w.Convert(result, IRDataTypes.Int64));
                return function;
            }

            this.SignedDivide = Signed("__sdiv64", false);
            this.SignedRemainder = Signed("__srem64", true);
        }

        void BuildClock()
        {
            this.Ticks = Function("__ticks", IRDataTypes.UInt64);
            X8664RuntimeWriter w = Writer(this.Ticks);

            Write32(w, TimerOperation, w.UInt32(1u << 30));
            w.While(w.Bool(true), () =>
            {
                w.If(NotEqual(w, And(w, Read32(w, TimerOperation), w.UInt32(1u << 29)), w.UInt32(0)), () => w.Builder.BuildBreak());
            });

            IRLocal high = w.Local("high", new IRType(IRDataTypes.UInt64), w.Convert(Read32(w, TimerHigh), IRDataTypes.UInt64));
            IRLocal low = w.Local("low", new IRType(IRDataTypes.UInt64), w.Convert(Read32(w, TimerLow), IRDataTypes.UInt64));
            w.Return(Or(w, ShiftLeft(w, high, w.UInt64(32)), low));

            this.Millis = Function("__system_millis", IRDataTypes.Int64);
            w = Writer(this.Millis);
            w.Return(w.Convert(w.Divide(w.Subtract(w.Call(this.Ticks), this.TimeStart), w.UInt64(TimerTicksPerMillisecond)), IRDataTypes.Int64));

            this.Sleep = Function("__system_sleep", IRDataTypes.Void, ("milliseconds", IRDataTypes.Int32));
            w = Writer(this.Sleep);

            w.If(Greater(w, w.Parameter(0), w.Int32(0)), () =>
            {
                IRLocal until = w.Local("until", new IRType(IRDataTypes.UInt64), w.Add(w.Call(this.Ticks), w.Multiply(w.Convert(w.Parameter(0), IRDataTypes.UInt64), w.UInt64(TimerTicksPerMillisecond))));
                w.While(w.Bool(true), () =>
                {
                    w.If(GreaterOrEqual(w, w.Call(this.Ticks), until), () => w.Builder.BuildBreak());
                });
            });

            w.Return();

            this.Entropy = Function("__system_entropy", IRDataTypes.UInt64);
            w = Writer(this.Entropy);

            IRLocal first = w.Local("first", new IRType(IRDataTypes.UInt64), w.Convert(Read32(w, RandomData), IRDataTypes.UInt64));
            IRLocal ticks = w.Local("ticks", new IRType(IRDataTypes.UInt64), w.Call(this.Ticks));
            IRLocal second = w.Local("second", new IRType(IRDataTypes.UInt64), w.Convert(Read32(w, RandomData), IRDataTypes.UInt64));
            w.Return(w.Builder.BuildBitwise(IRBitwiseOperator.Xor, Or(w, ShiftLeft(w, first, w.UInt64(32)), second), ticks));
        }

        void BuildLog()
        {
            this.Log = Function("__system_log", IRDataTypes.Void, ("line", IRDataTypes.String));
            X8664RuntimeWriter w = Writer(this.Log);

            w.Invoke(this.SerialWrite, w.Parameter(0));
            Text(w, "\r\n");
            w.Return();

            this.Write = Function("__system_write", IRDataTypes.Void, ("text", IRDataTypes.String));
            X8664RuntimeWriter write = Writer(this.Write);
            write.Invoke(this.SerialWrite, write.Parameter(0));
            write.Return();
        }

        void BuildException()
        {
            this.Exception = Function("__exception", IRDataTypes.Void, ("cause", IRDataTypes.UInt32), ("pc", IRDataTypes.UInt32), ("address", IRDataTypes.UInt32));
            X8664RuntimeWriter w = Writer(this.Exception);

            (uint Cause, string Name)[] names =
            [
                (0, "illegal instruction"),
                (2, "instruction fetch error"),
                (3, "load or store error"),
                (6, "integer divide by zero"),
                (9, "unaligned load or store"),
                (20, "instruction fetch from a forbidden address"),
                (28, "load from a forbidden address"),
                (29, "store to a forbidden address"),
            ];

            Text(w, "\r\nCrash: ");

            foreach ((uint cause, string name) in names)
            {
                w.If(Equal(w, w.Parameter(0), w.UInt32(cause)), () => Text(w, name + " "));
            }

            Text(w, "(cause ");
            w.Invoke(this.SerialNumber, w.Parameter(0), w.UInt32(10));
            Text(w, ") at 0x");
            w.Invoke(this.SerialNumber, w.Parameter(1), w.UInt32(16));
            Text(w, ", address 0x");
            w.Invoke(this.SerialNumber, w.Parameter(2), w.UInt32(16));
            Text(w, "\r\n");
            w.Invoke(this.Halt, w.Int32(2));
            w.Return();
        }

        void BuildStart(IRFunction entryPoint, List<IRGlobal> globals)
        {
            this.Start = Function("__start", IRDataTypes.Void);
            X8664RuntimeWriter w = Writer(this.Start);

            Text(w, "\u0002");

            w.Set(this.HeapStart, this.HeapNext);
            w.Set(this.Bins, w.Call(this.Module.Functions.First(f => f.Name == "__heap_bump"), w.UInt32(4 * (LargestClass + 1))));

            IRLocal i = w.Local("i", new IRType(IRDataTypes.UInt32), w.UInt32(0));

            w.While(Less(w, i, w.UInt32(LargestClass + 1)), () =>
            {
                w.Write(w.Add(this.Bins, w.Multiply(i, w.UInt32(4))), 0, w.Pointer(0));
                w.Set(i, w.Add(i, w.UInt32(1)));
            });

            w.Set(this.TimeStart, w.Call(this.Ticks));

            HashSet<IRGlobal> companions = globals.Where(g => g.LengthCompanion != null).Select(g => g.LengthCompanion!).ToHashSet();

            foreach (IRGlobal global in globals)
            {
                if (companions.Contains(global) || global.InitialValue is IRConstantInteger { Value: 0 })
                {
                    continue;
                }

                w.Set(global, global.InitialValue);
            }

            List<IRValueInstruction> arguments = new List<IRValueInstruction>();

            if (entryPoint.Parameters.Count == 1)
            {
                IRType argumentsType = entryPoint.Parameters[0].ValueType;

                if (argumentsType.DataType != IRDataTypes.Array || argumentsType.UserObject is not IRArraySchema schema)
                {
                    throw ErrorHandler.Create("The parameter of the entry point must be an array of strings.");
                }

                arguments.Add(w.Builder.BuildInstantiateArray(schema, w.Int32(0)));
            }
            else if (entryPoint.Parameters.Count > 1)
            {
                throw ErrorHandler.Create($"The entry point '{entryPoint.Name}' must take no parameters or a single array of strings.");
            }

            IRValueInstruction result = w.Call(entryPoint, arguments.ToArray());

            IRValueInstruction exitCode = entryPoint.ReturnTypes.Count == 0 || entryPoint.ReturnTypes[0].DataType == IRDataTypes.Void
                ? w.Int32(0)
                : w.Convert(result, IRDataTypes.Int32);

            w.Invoke(this.Halt, exitCode);
            w.Return();
        }
    }
}
