using CommonIR.Errors;
using CommonIR.Generators.Binary.DXIL;
using CommonIR.Generators.Binary.X8664.Emission;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Memory;
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

        public IRFunction Free { get; private set; } = null!;

        /// <summary>
        /// The bounds of the executable image, where constant data lives. Set by <see cref="Start"/>.
        /// </summary>
        IRGlobal ImageStart { get; set; } = null!;

        IRGlobal ImageEnd { get; set; } = null!;

        /// <summary>
        /// The module's live allocation counter, if it has one (see <see cref="IRFree.LiveAllocationsGlobal"/>).
        /// </summary>
        IRGlobal? LiveAllocations { get; set; }

        public IRFunction Panic { get; private set; } = null!;

        public IRFunction Start { get; private set; } = null!;

        public Dictionary<IRFunctionImport, IRGlobal> SystemImports { get; } = new Dictionary<IRFunctionImport, IRGlobal>();

        public List<SourceFile> ShaderSources { get; } = new List<SourceFile>();

        const uint LoadLibrarySearchSystem32 = 0x800;

        public const string RuntimeModule = "commonir";

        static bool IsSystemLibrary(string library)
        {
            if (library == "kernel32.dll" || library.IndexOfAny(['\\', '/', ':']) >= 0)
            {
                return false;
            }

            string directory = Environment.SystemDirectory;
            return !string.IsNullOrEmpty(directory) && System.IO.File.Exists(System.IO.Path.Combine(directory, library));
        }

        void ResolveSystemImports(X8664RuntimeWriter w)
        {
            foreach (IRFunctionImport functionImport in this.Module.FunctionImports.ToList())
            {
                string library = functionImport.ModuleName.ToLowerInvariant();

                if (IsSystemLibrary(library) && !this.SystemImports.ContainsKey(functionImport))
                {
                    this.SystemImports[functionImport] = this.Module.CreateGlobal($"__system_import.{library}!{functionImport.Name}", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
                }
            }

            foreach (IGrouping<string, KeyValuePair<IRFunctionImport, IRGlobal>> library in this.SystemImports.GroupBy(i => i.Key.ModuleName.ToLowerInvariant()))
            {
                IRLocal handle = w.Local($"library.{library.Key}", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "LoadLibraryExA", IRDataTypes.Pointer, w.DataPointer(w.Builder.BuildConstantString(library.Key)), w.Pointer(0), w.UInt32(LoadLibrarySearchSystem32)));

                w.If(w.Compare(IRComparisonOperator.EqualTo, handle, w.Pointer(0)), () =>
                {
                    w.Invoke(this.Panic, w.Builder.BuildConstantString($"Could not load {library.Key}."));
                });

                foreach ((IRFunctionImport functionImport, IRGlobal slot) in library)
                {
                    w.Set(slot, w.Native("kernel32.dll", "GetProcAddress", IRDataTypes.Pointer, handle, w.DataPointer(w.Builder.BuildConstantString(functionImport.Name))));

                    w.If(w.Compare(IRComparisonOperator.EqualTo, slot, w.Pointer(0)), () =>
                    {
                        w.Invoke(this.Panic, w.Builder.BuildConstantString($"Could not find {functionImport.Name} in {library.Key}."));
                    });
                }
            }
        }

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
            this.ImageStart = this.Module.CreateGlobal("__image_start", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
            this.ImageEnd = this.Module.CreateGlobal("__image_end", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
            this.LiveAllocations = this.Module.Globals.FirstOrDefault(g => g.Name == IRFree.LiveAllocationsGlobal);

            BuildPanic();
            BuildMalloc();
            BuildFree();

            context.Malloc = this.Malloc;
            context.Free = this.Free;
            context.Panic = this.Panic;

            if (kernels.Count == 0 && this.Module.FunctionImports.Any(i => i.ModuleName == RuntimeModule && !IsResidencyImport(i)))
            {
                throw ErrorHandler.Create("Showing frames in a window needs a parallel function that draws them.");
            }

            if (kernels.Count == 0)
            {
                foreach (IRFunctionImport functionImport in this.Module.FunctionImports.Where(i => i.ModuleName == RuntimeModule && IsResidencyImport(i)))
                {
                    context.RuntimeImports[functionImport] = NothingToDo(functionImport);
                }
            }

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

                foreach (IRFunctionImport functionImport in this.Module.FunctionImports.Where(i => i.ModuleName == RuntimeModule))
                {
                    context.RuntimeImports[functionImport] = functionImport.Name switch
                    {
                        "surface" => gpu.Surface,
                        "present" => gpu.Present,
                        "resize" => gpu.Resize,
                        "sync" => gpu.Sync,
                        "forget" => gpu.Forget,
                        _ => throw ErrorHandler.Create($"The runtime does not provide '{functionImport.Name}'."),
                    };
                }

                foreach ((IRFunction kernel, IRFunction dispatcher) in gpu.Dispatchers)
                {
                    context.Dispatchers[kernel] = dispatcher;
                }
            }

            BuildStart(entryPoint, globals);
        }

        static bool IsResidencyImport(IRFunctionImport functionImport) => functionImport.Name is "sync" or "forget";

        IRFunction NothingToDo(IRFunctionImport functionImport)
        {
            IRFunction function = this.Module.CreateFunction($"__gpu_{functionImport.Name}_unused", [new IRType(IRDataTypes.Void)], functionImport.Parameters.Select(p => new IRLocal(p.Name, p.ValueType, isMutable: false)).ToList(), isExport: false);
            Writer(function).Return();
            return function;
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

            if (this.LiveAllocations != null)
            {
                AdjustLiveAllocations(w, w.Add(this.LiveAllocations, w.Int64(1)));
            }

            w.Return(result);
        }

        /// <summary>
        /// Releases a block from <see cref="Malloc"/>. Null and addresses inside the executable image (constant data)
        /// are ignored.
        /// </summary>
        void BuildFree()
        {
            this.Free = this.Module.CreateFunction("__free", [new IRType(IRDataTypes.Void)], [new IRLocal("pointer", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Free);

            IRLocal pointer = w.Parameter(0);

            w.If(w.Compare(IRComparisonOperator.EqualTo, pointer, w.Pointer(0)), () => w.Return());
            w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, w.Convert(pointer, IRDataTypes.UInt64), w.Convert(this.ImageStart, IRDataTypes.UInt64)), () =>
            {
                w.If(w.Compare(IRComparisonOperator.LessThan, w.Convert(pointer, IRDataTypes.UInt64), w.Convert(this.ImageEnd, IRDataTypes.UInt64)), () => w.Return());
            });

            w.Native("kernel32.dll", "HeapFree", IRDataTypes.Int32, this.Heap, w.UInt32(0), pointer);

            if (this.LiveAllocations != null)
            {
                AdjustLiveAllocations(w, w.Subtract(this.LiveAllocations, w.Int64(1)));
            }

            w.Return();
        }

        void AdjustLiveAllocations(X8664RuntimeWriter w, IRValueInstruction value)
        {
            if (this.Module.IsMultithreaded)
            {
                w.Native("kernel32.dll", "HeapLock", IRDataTypes.Int32, this.Heap);
            }

            w.Set(this.LiveAllocations!, value);

            if (this.Module.IsMultithreaded)
            {
                w.Native("kernel32.dll", "HeapUnlock", IRDataTypes.Int32, this.Heap);
            }
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

            w.If(w.Compare(IRComparisonOperator.EqualTo, vector, w.Pointer(0)), () =>
            {
                w.Set(count, w.Int32(0));
            });

            IRLocal result = w.Local("result", argumentsType, w.Builder.BuildInstantiateArray(schema, count));
            IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

            w.While(w.Compare(IRComparisonOperator.LessThan, i, count), () =>
            {
                IRLocal wide = w.Local("wide", new IRType(IRDataTypes.Pointer), w.ReadAt(vector, w.Multiply(w.Convert(w.Add(i, w.Int32(1)), IRDataTypes.Int64), w.Int64(8)), IRDataTypes.Pointer));
                IRLocal size = w.Local("size", new IRType(IRDataTypes.Int32), w.Native("kernel32.dll", "WideCharToMultiByte", IRDataTypes.Int32, w.UInt32(65001), w.UInt32(0), wide, w.Int32(-1), w.Pointer(0), w.Int32(0), w.Pointer(0), w.Pointer(0)));

                w.If(w.Compare(IRComparisonOperator.LessThan, size, w.Int32(1)), () =>
                {
                    w.Set(size, w.Int32(1));
                });

                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.Pointer), w.Call(this.Malloc, w.Convert(w.Add(size, w.Int32(1)), IRDataTypes.Int64)));

                w.If(w.Compare(IRComparisonOperator.EqualTo, w.Native("kernel32.dll", "WideCharToMultiByte", IRDataTypes.Int32, w.UInt32(65001), w.UInt32(0), wide, w.Int32(-1), bytes, size, w.Pointer(0), w.Pointer(0)), w.Int32(0)), () =>
                {
                    w.Set(size, w.Int32(1));
                });
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

            // The image spans SizeOfImage bytes (in the optional header, at e_lfanew + 0x50) from its base address.
            w.Set(this.ImageStart, w.Native("kernel32.dll", "GetModuleHandleW", IRDataTypes.Pointer, w.Pointer(0)));
            IRValueInstruction headers = w.Add(this.ImageStart, w.Convert(w.Read(this.ImageStart, 0x3C, IRDataTypes.UInt32), IRDataTypes.Int64));
            w.Set(this.ImageEnd, w.Add(this.ImageStart, w.Convert(w.Read(headers, 0x50, IRDataTypes.UInt32), IRDataTypes.Int64)));
            w.Set(this.Scratch, w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Heap, w.UInt32(8), w.Int64(256)));

            IRGlobal crashBuffer = this.Module.CreateGlobal("__crash_buffer", new IRType(IRDataTypes.Pointer), new IRConstantInteger(IRDataTypes.Pointer, 0), isMutable: true);
            w.Set(crashBuffer, w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Heap, w.UInt32(8), w.Int64(16)));

            ResolveSystemImports(w);

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
