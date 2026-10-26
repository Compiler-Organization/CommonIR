using CommonIR.Errors;
using CommonIR.Generators.Binary.DXIL;
using CommonIR.Generators.Binary.X8664.Emission;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Runtime
{
    internal class X8664RuntimeBuilder
    {
        IRModule Module { get; set; }

        CommonIRX8664Configuration Configuration { get; set; }

        Dictionary<string, IRFunctionImport> Imports { get; set; } = new Dictionary<string, IRFunctionImport>();

        public IRGlobal Heap { get; private set; } = null!;

        public IRGlobal Scratch { get; private set; } = null!;

        public IRFunction Malloc { get; private set; } = null!;

        public IRFunction Panic { get; private set; } = null!;

        public IRFunction Start { get; private set; } = null!;

        public List<SourceFile> ShaderSources { get; } = new List<SourceFile>();

        public X8664RuntimeBuilder(IRModule module, CommonIRX8664Configuration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;

            foreach (IRFunctionImport functionImport in module.FunctionImports)
            {
                this.Imports.TryAdd($"{functionImport.ModuleName.ToLowerInvariant()}!{functionImport.Name}", functionImport);
            }
        }

        X8664RuntimeWriter Writer(IRFunction function) => new X8664RuntimeWriter(this.Module, function, this.Imports);

        public void Build(X8664Context context)
        {
            IRFunction entryPoint = this.Module.EntryPoint ?? throw ErrorHandler.Create("An entry point is required when targeting x86-64.");

            List<IRGlobal> globals = this.Module.Globals.ToList();
            List<IRFunction> kernels = this.Module.Functions.Where(f => f.IsKernel).ToList();

            this.Heap = this.Module.CreateGlobal("__heap", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
            this.Scratch = this.Module.CreateGlobal("__scratch", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);

            BuildPanic();
            BuildMalloc();

            context.Malloc = this.Malloc;
            context.Panic = this.Panic;

            if (kernels.Count > 0)
            {
                DXILCompiler compiler = new DXILCompiler(this.Configuration.DXCPath);
                List<(IRFunction Kernel, byte[] Shader)> shaders = new List<(IRFunction Kernel, byte[] Shader)>();

                foreach (IRFunction kernel in kernels)
                {
                    string source = HLSLKernelTranslator.Translate(kernel, this.Configuration.Shaders);
                    string name = new string(kernel.Name.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());

                    if (this.Configuration.EmitShaderSources)
                    {
                        this.ShaderSources.Add(new SourceFile($"{this.Module.Name}_{name}", ".hlsl", Encoding.UTF8.GetBytes(source)));
                    }

                    shaders.Add((kernel, compiler.Compile(source, this.Configuration.Shaders.EntryPointName, this.Configuration.Shaders.ShaderTarget, name)));
                }

                X8664GpuRuntimeBuilder gpu = new X8664GpuRuntimeBuilder(this.Module, this, this.Imports, this.Configuration);
                gpu.Build(shaders);

                context.Await = gpu.Await;

                foreach ((IRFunction kernel, IRFunction dispatcher) in gpu.Dispatchers)
                {
                    context.Dispatchers[kernel] = dispatcher;
                }
            }

            BuildStart(entryPoint, globals);
        }

        void BuildPanic()
        {
            this.Panic = this.Module.CreateFunction("__panic", [new IRType(IRDataTypes.Void)], [new IRLocal("message", IRDataTypes.String, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Panic);

            IRLocal message = w.Parameter(0);
            IRLocal handle = w.Local("handle", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetStdHandle", IRDataTypes.Pointer, w.Int32(-12)));
            IRValueInstruction newLine = w.Builder.BuildConstantString("\r\n");

            w.Native("kernel32.dll", "WriteFile", IRDataTypes.Int32, handle, w.DataPointer(message), w.Length(message), this.Scratch, w.Pointer(0));
            w.Native("kernel32.dll", "WriteFile", IRDataTypes.Int32, handle, w.DataPointer(newLine), w.Length(newLine), this.Scratch, w.Pointer(0));
            w.Native("kernel32.dll", "ExitProcess", IRDataTypes.Void, w.UInt32(1));
            w.Return();
        }

        IRFunction BuildCrashHandler(IRGlobal buffer)
        {
            IRFunction handler = this.Module.CreateFunction("__crash", [new IRType(IRDataTypes.Int32)], [new IRLocal("information", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(handler);

            IRLocal code = w.Local("code", new IRType(IRDataTypes.UInt32), w.Read(w.Read(w.Parameter(0), 0, IRDataTypes.Pointer), 0, IRDataTypes.UInt32));
            IRLocal handle = w.Local("handle", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetStdHandle", IRDataTypes.Pointer, w.Int32(-12)));

            void Write(string text)
            {
                IRValueInstruction message = w.Builder.BuildConstantString(text);
                w.Native("kernel32.dll", "WriteFile", IRDataTypes.Int32, handle, w.DataPointer(message), w.Length(message), this.Scratch, w.Pointer(0));
            }

            w.If(w.Compare(IRComparisonOperator.EqualTo, code, w.UInt32(0xC00000FD)), () => Write("Stack overflow."), () =>
            {
                w.If(w.Compare(IRComparisonOperator.EqualTo, code, w.UInt32(0xC0000005)), () => Write("Access violation: the program used an invalid memory address."), () =>
                {
                    Write("Unhandled exception 0x");

                    IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

                    w.While(w.Compare(IRComparisonOperator.LessThan, i, w.Int32(8)), () =>
                    {
                        IRValueInstruction shift = w.Convert(w.Subtract(w.Int32(28), w.Multiply(i, w.Int32(4))), IRDataTypes.UInt32);
                        IRLocal nibble = w.Local("nibble", new IRType(IRDataTypes.UInt32), w.Builder.BuildBitwise(IRBitwiseOperator.And, w.Builder.BuildBitwise(IRBitwiseOperator.ShiftRight, code, shift), w.UInt32(15)));
                        IRLocal character = w.Local("character", new IRType(IRDataTypes.UInt32), w.Add(nibble, w.UInt32('0')));

                        w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, nibble, w.UInt32(10)), () => w.Set(character, w.Add(nibble, w.UInt32('A' - 10))));

                        w.Builder.BuildStore(buffer, w.Convert(i, IRDataTypes.Int64), w.Convert(character, IRDataTypes.UInt8));
                        w.Set(i, w.Add(i, w.Int32(1)));
                    });

                    w.Native("kernel32.dll", "WriteFile", IRDataTypes.Int32, handle, buffer, w.Int32(8), this.Scratch, w.Pointer(0));
                    Write(".");
                });
            });

            Write("\r\n");
            w.Native("kernel32.dll", "ExitProcess", IRDataTypes.Void, code);
            w.Return(w.Int32(0));

            return handler;
        }

        void BuildMalloc()
        {
            this.Malloc = this.Module.CreateFunction("__malloc", [new IRType(IRDataTypes.Pointer)], [new IRLocal("bytes", IRDataTypes.Int64, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Malloc);

            IRLocal result = w.Local("result", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Heap, w.UInt32(8), w.Parameter(0)));

            w.If(w.Compare(IRComparisonOperator.EqualTo, result, w.Pointer(0)), () =>
            {
                w.Invoke(this.Panic, w.Builder.BuildConstantString("Out of memory."));
            });

            w.Return(result);
        }

        IRFunction BuildArguments(IRType argumentsType)
        {
            if (argumentsType.DataType != IRDataTypes.Array || argumentsType.UserObject is not IRArraySchema schema || schema.ElementType.DataType != IRDataTypes.String)
            {
                throw ErrorHandler.Create("The parameter of the entry point must be an array of strings.");
            }

            IRFunction arguments = this.Module.CreateFunction("__arguments", [argumentsType], [], isExport: false);
            X8664RuntimeWriter w = Writer(arguments);

            IRValueInstruction commandLine = w.Native("kernel32.dll", "GetCommandLineW", IRDataTypes.Pointer);
            IRLocal vector = w.Local("vector", new IRType(IRDataTypes.Pointer), w.Native("shell32.dll", "CommandLineToArgvW", IRDataTypes.Pointer, commandLine, this.Scratch));
            IRLocal count = w.Local("count", new IRType(IRDataTypes.Int32), w.Subtract(w.Read(this.Scratch, 0, IRDataTypes.Int32), w.Int32(1)));

            w.If(w.Compare(IRComparisonOperator.LessThan, count, w.Int32(0)), () =>
            {
                w.Set(count, w.Int32(0));
            });

            IRLocal result = w.Local("result", argumentsType, w.Builder.BuildInstantiateArray(schema, count));
            IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

            w.While(w.Compare(IRComparisonOperator.LessThan, i, count), () =>
            {
                IRLocal wide = w.Local("wide", new IRType(IRDataTypes.Pointer), w.ReadAt(vector, w.Multiply(w.Convert(w.Add(i, w.Int32(1)), IRDataTypes.Int64), w.Int64(8)), IRDataTypes.Pointer));
                IRLocal size = w.Local("size", new IRType(IRDataTypes.Int32), w.Native("kernel32.dll", "WideCharToMultiByte", IRDataTypes.Int32, w.UInt32(65001), w.UInt32(0), wide, w.Int32(-1), w.Pointer(0), w.Int32(0), w.Pointer(0), w.Pointer(0)));
                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.Pointer), w.Call(this.Malloc, w.Convert(w.Add(size, w.Int32(1)), IRDataTypes.Int64)));

                w.Native("kernel32.dll", "WideCharToMultiByte", IRDataTypes.Int32, w.UInt32(65001), w.UInt32(0), wide, w.Int32(-1), bytes, size, w.Pointer(0), w.Pointer(0));
                w.Builder.BuildStoreElement(result, i, w.FatPointer(bytes, w.Convert(w.Subtract(size, w.Int32(1)), IRDataTypes.Int64), schema.ElementType), schema.ElementType);
                w.Set(i, w.Add(i, w.Int32(1)));
            });

            w.Native("kernel32.dll", "LocalFree", IRDataTypes.Pointer, vector);
            w.Return(result);

            return arguments;
        }

        void BuildStart(IRFunction entryPoint, List<IRGlobal> globals)
        {
            this.Start = this.Module.CreateFunction("__start", [new IRType(IRDataTypes.Void)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.Start);

            w.Set(this.Heap, w.Native("kernel32.dll", "GetProcessHeap", IRDataTypes.Pointer));
            w.Set(this.Scratch, w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Heap, w.UInt32(8), w.Int64(256)));

            IRGlobal crashBuffer = this.Module.CreateGlobal("__crash_buffer", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
            w.Set(crashBuffer, w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Heap, w.UInt32(8), w.Int64(16)));

            w.Write(this.Scratch, 0, w.UInt32(65536));
            w.Native("kernel32.dll", "SetThreadStackGuarantee", IRDataTypes.Int32, this.Scratch);
            w.Native("kernel32.dll", "SetUnhandledExceptionFilter", IRDataTypes.Pointer, w.Builder.BuildFunctionReference(BuildCrashHandler(crashBuffer)));

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
                arguments.Add(w.Call(BuildArguments(entryPoint.Parameters[0].ValueType)));
            }
            else if (entryPoint.Parameters.Count > 1)
            {
                throw ErrorHandler.Create($"The entry point '{entryPoint.Name}' must take no parameters or a single array of strings.");
            }

            IRValueInstruction result = w.Call(entryPoint, arguments.ToArray());

            IRValueInstruction exitCode = entryPoint.ReturnTypes.Count == 0 || entryPoint.ReturnTypes[0].DataType == IRDataTypes.Void
                ? w.UInt32(0)
                : w.Convert(result, IRDataTypes.UInt32);

            w.Native("kernel32.dll", "ExitProcess", IRDataTypes.Void, exitCode);
            w.Return();
        }
    }
}
