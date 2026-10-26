using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Runtime
{
    internal class X8664GpuRuntimeBuilder
    {
        const int HandleSize = 80;
        const int HandleKind = 0;
        const int HandleInputCount = 4;
        const int HandleReadback = 8;
        const int HandleOutput = 16;
        const int HandleAllocator = 24;
        const int HandleList = 32;
        const int HandleFenceValue = 40;
        const int HandleCount = 48;
        const int HandleStride = 52;
        const int HandleInputs = 56;
        const int HandleResult = 64;

        const int KindPending = 1;
        const int KindCompleted = 2;

        const int DeviceCreateCommandQueue = 8;
        const int DeviceCreateCommandAllocator = 9;
        const int DeviceCreateComputePipelineState = 11;
        const int DeviceCreateCommandList = 12;
        const int DeviceCreateRootSignature = 16;
        const int DeviceCreateCommittedResource = 27;
        const int DeviceCreateFence = 36;

        const int QueueExecuteCommandLists = 10;
        const int QueueSignal = 14;

        const int ListClose = 9;
        const int ListDispatch = 14;
        const int ListCopyResource = 17;
        const int ListResourceBarrier = 26;
        const int ListSetComputeRootSignature = 29;
        const int ListSetComputeRoot32BitConstants = 35;
        const int ListSetComputeRootShaderResourceView = 39;
        const int ListSetComputeRootUnorderedAccessView = 41;

        const int ResourceMap = 8;
        const int ResourceUnmap = 9;
        const int ResourceGetGPUVirtualAddress = 11;

        const int FenceGetCompletedValue = 8;
        const int FenceSetEventOnCompletion = 9;

        const int UnknownRelease = 2;

        const int HeapDefault = 1;
        const int HeapUpload = 2;
        const int HeapReadback = 3;

        const int StateCommon = 0x0;
        const int StateUnorderedAccess = 0x8;
        const int StateCopyDestination = 0x400;
        const int StateCopySource = 0x800;
        const int StateGenericRead = 0xAC3;

        const int FlagAllowUnorderedAccess = 0x4;

        const int CommandListTypeCompute = 2;

        const int FeatureLevel11 = 0xB000;

        static readonly string[] InterfaceIdentifiers =
        [
            "189819f1-1db6-4b57-be54-1821339b85f7",
            "0ec870a6-5d7e-4c22-8cfc-5baae07616ed",
            "6102dee4-af59-4b09-b999-b44d73f09b24",
            "5b160d0f-ac1b-4185-8ba8-b3ae42a5a455",
            "0a753dcf-c4d8-4b91-adf6-be5a60d95a76",
            "696442be-a72e-4059-bc79-5b5c98040fad",
            "c54a6b66-72df-4ee8-8be5-a946a1429214",
            "765a30f3-f624-4c6f-a828-ace948622445",
        ];

        enum Interfaces
        {
            Device,
            CommandQueue,
            CommandAllocator,
            GraphicsCommandList,
            Fence,
            Resource,
            RootSignature,
            PipelineState,
        }

        IRModule Module { get; set; }

        X8664RuntimeBuilder Runtime { get; set; }

        Dictionary<string, IRFunctionImport> Imports { get; set; }

        CommonIRX8664Configuration Configuration { get; set; }

        IRGlobal State { get; set; } = null!;
        IRGlobal Device { get; set; } = null!;
        IRGlobal Queue { get; set; } = null!;
        IRGlobal Fence { get; set; } = null!;
        IRGlobal FenceValue { get; set; } = null!;
        IRGlobal Event { get; set; } = null!;
        IRGlobal Descriptor { get; set; } = null!;

        IRConstantBytes Identifiers { get; set; } = null!;

        IRFunction Initialize { get; set; } = null!;
        IRFunction CreateBuffer { get; set; } = null!;
        IRFunction Release { get; set; } = null!;

        public IRFunction Await { get; private set; } = null!;

        public Dictionary<IRFunction, IRFunction> Dispatchers { get; } = new Dictionary<IRFunction, IRFunction>();

        public X8664GpuRuntimeBuilder(IRModule module, X8664RuntimeBuilder runtime, Dictionary<string, IRFunctionImport> imports, CommonIRX8664Configuration configuration)
        {
            this.Module = module;
            this.Runtime = runtime;
            this.Imports = imports;
            this.Configuration = configuration;
        }

        X8664RuntimeWriter Writer(IRFunction function) => new X8664RuntimeWriter(this.Module, function, this.Imports);

        IRGlobal Global(string name, IRDataTypes dataType) => this.Module.CreateGlobal(name, new IRType(dataType), new IRConstantInteger(dataType, 0), isMutable: true);

        IRValueInstruction Identifier(X8664RuntimeWriter writer, Interfaces identifier) => writer.Add(writer.DataPointer(this.Identifiers), writer.Int64(16 * (int)identifier));

        public void Build(List<(IRFunction Kernel, byte[] Shader)> kernels)
        {
            this.State = Global("__gpu_state", IRDataTypes.Int32);
            this.Device = Global("__gpu_device", IRDataTypes.Pointer);
            this.Queue = Global("__gpu_queue", IRDataTypes.Pointer);
            this.Fence = Global("__gpu_fence", IRDataTypes.Pointer);
            this.FenceValue = Global("__gpu_fence_value", IRDataTypes.UInt64);
            this.Event = Global("__gpu_event", IRDataTypes.Pointer);
            this.Descriptor = Global("__gpu_descriptor", IRDataTypes.Pointer);

            this.Identifiers = this.Module.CreateConstantBytes(InterfaceIdentifiers.SelectMany(i => new Guid(i).ToByteArray()).ToArray());

            BuildInitialize();
            BuildCreateBuffer();
            BuildRelease();
            BuildAwait();

            foreach ((IRFunction kernel, byte[] shader) in kernels)
            {
                this.Dispatchers[kernel] = BuildDispatcher(kernel, shader);
            }
        }

        void BuildInitialize()
        {
            this.Initialize = this.Module.CreateFunction("__gpu_initialize", [new IRType(IRDataTypes.Bool)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.Initialize);

            w.If(w.Compare(IRComparisonOperator.EqualTo, this.State, w.Int32(0)), () =>
            {
                w.Set(this.State, w.Int32(2));
                w.Set(this.Descriptor, w.Call(this.Runtime.Malloc, w.Int64(256)));

                IRLocal disabled = w.Local("disabled", new IRType(IRDataTypes.Int32), w.Native("kernel32.dll", "GetEnvironmentVariableA", IRDataTypes.Int32, w.DataPointer(w.Builder.BuildConstantString("COMMONIR_DISABLE_GPU")), this.Runtime.Scratch, w.UInt32(16)));

                w.If(w.Compare(IRComparisonOperator.EqualTo, disabled, w.Int32(0)), () =>
                {
                    IRLocal library = w.Local("library", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "LoadLibraryA", IRDataTypes.Pointer, w.DataPointer(w.Builder.BuildConstantString("d3d12.dll"))));

                    w.If(w.Compare(IRComparisonOperator.NotEqualTo, library, w.Pointer(0)), () =>
                    {
                        IRLocal createDevice = w.Local("createDevice", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetProcAddress", IRDataTypes.Pointer, library, w.DataPointer(w.Builder.BuildConstantString("D3D12CreateDevice"))));

                        w.If(w.Compare(IRComparisonOperator.NotEqualTo, createDevice, w.Pointer(0)), () =>
                        {
                            IRValueInstruction created = (IRValueInstruction)w.Builder.BuildCallIndirect(createDevice, new IRType(IRDataTypes.Int32), [w.Pointer(0), w.Int32(FeatureLevel11), Identifier(w, Interfaces.Device), this.Runtime.Scratch]);

                            w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, created, w.Int32(0)), () =>
                            {
                                w.Set(this.Device, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                                w.Write(this.Descriptor, 0, w.Int32(CommandListTypeCompute));
                                w.Write(this.Descriptor, 4, w.Int32(0));
                                w.Write(this.Descriptor, 8, w.Int32(0));
                                w.Write(this.Descriptor, 12, w.Int32(0));

                                IRValueInstruction queueCreated = w.Com(this.Device, DeviceCreateCommandQueue, IRDataTypes.Int32, this.Descriptor, Identifier(w, Interfaces.CommandQueue), this.Runtime.Scratch);

                                w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, queueCreated, w.Int32(0)), () =>
                                {
                                    w.Set(this.Queue, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                                    IRValueInstruction fenceCreated = w.Com(this.Device, DeviceCreateFence, IRDataTypes.Int32, w.UInt64(0), w.Int32(0), Identifier(w, Interfaces.Fence), this.Runtime.Scratch);

                                    w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, fenceCreated, w.Int32(0)), () =>
                                    {
                                        w.Set(this.Fence, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
                                        w.Set(this.Event, w.Native("kernel32.dll", "CreateEventA", IRDataTypes.Pointer, w.Pointer(0), w.Int32(0), w.Int32(0), w.Pointer(0)));
                                        w.Set(this.State, w.Int32(1));
                                    });
                                });
                            });
                        });
                    });
                });
            });

            w.Return(w.Compare(IRComparisonOperator.EqualTo, this.State, w.Int32(1)));
        }

        void BuildCreateBuffer()
        {
            this.CreateBuffer = this.Module.CreateFunction("__gpu_buffer", [new IRType(IRDataTypes.Pointer)],
            [
                new IRLocal("heap", IRDataTypes.Int32, isMutable: false),
                new IRLocal("bytes", IRDataTypes.UInt64, isMutable: false),
                new IRLocal("flags", IRDataTypes.Int32, isMutable: false),
                new IRLocal("state", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.CreateBuffer);
            IRGlobal d = this.Descriptor;

            w.Write(d, 0, w.Parameter(0));
            w.Write(d, 4, w.Int32(0));
            w.Write(d, 8, w.Int32(0));
            w.Write(d, 12, w.Int32(1));
            w.Write(d, 16, w.Int32(1));

            w.Write(d, 32, w.Int32(1));
            w.Write(d, 36, w.Int32(0));
            w.Write(d, 40, w.UInt64(0));
            w.Write(d, 48, w.Parameter(1));
            w.Write(d, 56, w.Int32(1));
            w.Write(d, 60, w.Int32(0x00010001));
            w.Write(d, 64, w.Int32(0));
            w.Write(d, 68, w.Int32(1));
            w.Write(d, 72, w.Int32(0));
            w.Write(d, 76, w.Int32(1));
            w.Write(d, 80, w.Parameter(2));

            IRValueInstruction created = w.Com(this.Device, DeviceCreateCommittedResource, IRDataTypes.Int32,
                d, w.Int32(0), w.Add(d, w.Int64(32)), w.Parameter(3), w.Pointer(0), Identifier(w, Interfaces.Resource), this.Runtime.Scratch);

            w.If(w.Compare(IRComparisonOperator.LessThan, created, w.Int32(0)), () =>
            {
                w.Invoke(this.Runtime.Panic, w.Builder.BuildConstantString("Could not allocate a GPU buffer."));
            });

            w.Return(w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
        }

        void BuildRelease()
        {
            this.Release = this.Module.CreateFunction("__gpu_release", [new IRType(IRDataTypes.Void)], [new IRLocal("instance", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Release);

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, w.Parameter(0), w.Pointer(0)), () =>
            {
                w.Com(w.Parameter(0), UnknownRelease, IRDataTypes.UInt32);
            });

            w.Return();
        }

        void Check(X8664RuntimeWriter w, IRValueInstruction result, string message)
        {
            w.If(w.Compare(IRComparisonOperator.LessThan, result, w.Int32(0)), () =>
            {
                w.Invoke(this.Runtime.Panic, w.Builder.BuildConstantString(message));
            });
        }

        void BuildAwait()
        {
            IRType arrayType = new IRType(IRDataTypes.Array, this.Module.CreateArraySchema(new IRType(IRDataTypes.UInt8)));

            this.Await = this.Module.CreateFunction("__gpu_await", [arrayType], [new IRLocal("handle", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Await);
            IRLocal handle = w.Parameter(0);
            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Read(handle, HandleKind, IRDataTypes.Int32), w.Int32(KindPending)), () =>
            {
                IRLocal value = w.Local("value", new IRType(IRDataTypes.UInt64), w.Read(handle, HandleFenceValue, IRDataTypes.UInt64));
                IRValueInstruction completed = w.Com(this.Fence, FenceGetCompletedValue, IRDataTypes.UInt64);

                w.If(w.Compare(IRComparisonOperator.LessThan, completed, value), () =>
                {
                    Check(w, w.Com(this.Fence, FenceSetEventOnCompletion, IRDataTypes.Int32, value, this.Event), "Could not wait for the GPU.");
                    w.Native("kernel32.dll", "WaitForSingleObject", IRDataTypes.UInt32, this.Event, w.UInt32(0xFFFFFFFF));
                });

                IRLocal readback = w.Local("readback", new IRType(IRDataTypes.Pointer), w.Read(handle, HandleReadback, IRDataTypes.Pointer));
                IRLocal result = w.Local("result", arrayType, w.Read(handle, HandleResult, arrayType));
                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(w.Read(handle, HandleCount, IRDataTypes.Int32), IRDataTypes.Int64), w.Convert(w.Read(handle, HandleStride, IRDataTypes.Int32), IRDataTypes.Int64)), IRDataTypes.UInt64));

                Check(w, w.Com(readback, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not read the results from the GPU.");
                w.Native("kernel32.dll", "RtlMoveMemory", IRDataTypes.Void, w.DataPointer(result), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer), bytes);

                w.Write(this.Descriptor, 0, w.UInt64(0));
                w.Write(this.Descriptor, 8, w.UInt64(0));
                w.Com(readback, ResourceUnmap, IRDataTypes.Void, w.UInt32(0), this.Descriptor);

                w.Invoke(this.Release, readback);
                w.Invoke(this.Release, w.Read(handle, HandleOutput, IRDataTypes.Pointer));
                w.Invoke(this.Release, w.Read(handle, HandleList, IRDataTypes.Pointer));
                w.Invoke(this.Release, w.Read(handle, HandleAllocator, IRDataTypes.Pointer));

                IRLocal inputs = w.Local("inputs", new IRType(IRDataTypes.Pointer), w.Read(handle, HandleInputs, IRDataTypes.Pointer));
                IRLocal inputCount = w.Local("inputCount", new IRType(IRDataTypes.Int32), w.Read(handle, HandleInputCount, IRDataTypes.Int32));
                IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

                w.While(w.Compare(IRComparisonOperator.LessThan, i, inputCount), () =>
                {
                    w.Invoke(this.Release, w.ReadAt(inputs, w.Multiply(w.Convert(i, IRDataTypes.Int64), w.Int64(8)), IRDataTypes.Pointer));
                    w.Set(i, w.Add(i, w.Int32(1)));
                });

                w.Write(handle, HandleKind, w.Int32(KindCompleted));
            });

            w.Return(w.Read(handle, HandleResult, arrayType));
        }

        IRFunction BuildDispatcher(IRFunction kernel, byte[] shader)
        {
            HLSLKernelLayout layout = new HLSLKernelLayout(kernel);
            int stride = HLSLKernelLayout.StrideOf(layout.OutputElementType, kernel.Name);
            int groupSize = this.Configuration.Shaders.ThreadGroupSizes.X;

            IRGlobal rootSignature = Global($"__gpu_root_signature.{kernel.Name}", IRDataTypes.Pointer);
            IRGlobal pipeline = Global($"__gpu_pipeline.{kernel.Name}", IRDataTypes.Pointer);

            IRFunction prepare = BuildPrepare(kernel, shader, rootSignature, pipeline);

            List<IRLocal> parameters = [new IRLocal("count", IRDataTypes.Int32, isMutable: false), .. kernel.KernelParameters.Select(p => new IRLocal(p.Name, p.ValueType, isMutable: false))];
            IRFunction dispatcher = this.Module.CreateFunction($"__gpu_dispatch.{kernel.Name}", [new IRType(IRDataTypes.Pointer)], parameters, isExport: false);

            X8664RuntimeWriter w = Writer(dispatcher);

            IRLocal count = w.Parameter(0);
            List<IRLocal> arguments = dispatcher.Parameters.Skip(1).ToList();
            List<IRLocal> inputs = layout.Inputs.Select(input => arguments[kernel.KernelParameters.IndexOf(input)]).ToList();
            List<IRLocal> scalars = layout.Scalars.Select(scalar => arguments[kernel.KernelParameters.IndexOf(scalar)]).ToList();

            IRType outputType = kernel.KernelOutput.ValueType;
            IRLocal handle = w.Local("handle", new IRType(IRDataTypes.Pointer), w.Call(this.Runtime.Malloc, w.Int64(HandleSize)));
            IRLocal result = w.Local("result", outputType, w.Builder.BuildInstantiateArray((IRArraySchema)outputType.UserObject!, count));

            w.Write(handle, HandleCount, count);
            w.Write(handle, HandleStride, w.Int32(stride));
            w.Write(handle, HandleResult, result);

            IRLocal gpu = w.Local("gpu", new IRType(IRDataTypes.Bool), w.Bool(false));

            w.If(w.Compare(IRComparisonOperator.GreaterThan, count, w.Int32(0)), () =>
            {
                w.If(w.Call(this.Initialize), () =>
                {
                    w.If(w.Call(prepare), () =>
                    {
                        w.Set(gpu, w.Bool(true));
                    });
                });
            });

            w.If(gpu, () =>
            {
                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(count, IRDataTypes.Int64), w.Int64(stride)), IRDataTypes.UInt64));
                IRLocal output = w.Local("output", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapDefault), bytes, w.Int32(FlagAllowUnorderedAccess), w.Int32(StateCommon)));
                IRLocal readback = w.Local("readback", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapReadback), bytes, w.Int32(0), w.Int32(StateCopyDestination)));
                IRLocal uploads = w.Local("uploads", new IRType(IRDataTypes.Pointer), w.Call(this.Runtime.Malloc, w.Int64(8 * Math.Max(1, inputs.Count))));
                List<IRLocal> uploadLocals = new List<IRLocal>();

                for (int j = 0; j < inputs.Count; j++)
                {
                    IRLocal input = inputs[j];
                    int inputStride = HLSLKernelLayout.StrideOf(HLSLKernelLayout.ElementTypeOf(input.ValueType, kernel.Name), kernel.Name);

                    IRLocal size = w.Local($"size{j}", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(w.Length(input), IRDataTypes.Int64), w.Int64(inputStride)), IRDataTypes.UInt64));
                    IRLocal allocation = w.Local($"allocation{j}", new IRType(IRDataTypes.UInt64), size);

                    w.If(w.Compare(IRComparisonOperator.EqualTo, allocation, w.UInt64(0)), () =>
                    {
                        w.Set(allocation, w.UInt64(inputStride));
                    });

                    IRLocal upload = w.Local($"upload{j}", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapUpload), allocation, w.Int32(0), w.Int32(StateGenericRead)));
                    uploadLocals.Add(upload);
                    w.Write(uploads, 8 * j, upload);

                    w.If(w.Compare(IRComparisonOperator.GreaterThan, size, w.UInt64(0)), () =>
                    {
                        Check(w, w.Com(upload, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not upload data to the GPU.");
                        w.Native("kernel32.dll", "RtlMoveMemory", IRDataTypes.Void, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer), w.DataPointer(input), size);
                        w.Com(upload, ResourceUnmap, IRDataTypes.Void, w.UInt32(0), w.Pointer(0));
                    });
                }

                Check(w, w.Com(this.Device, DeviceCreateCommandAllocator, IRDataTypes.Int32, w.Int32(CommandListTypeCompute), Identifier(w, Interfaces.CommandAllocator), this.Runtime.Scratch), "Could not create a GPU command allocator.");
                IRLocal allocator = w.Local("allocator", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                Check(w, w.Com(this.Device, DeviceCreateCommandList, IRDataTypes.Int32, w.UInt32(0), w.Int32(CommandListTypeCompute), allocator, pipeline, Identifier(w, Interfaces.GraphicsCommandList), this.Runtime.Scratch), "Could not create a GPU command list.");
                IRLocal list = w.Local("list", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                IRLocal groups = w.Local("groups", new IRType(IRDataTypes.Int32), w.Divide(w.Add(count, w.Int32(groupSize - 1)), w.Int32(groupSize)));
                IRLocal columns = w.Local("columns", new IRType(IRDataTypes.Int32), groups);
                IRLocal rows = w.Local("rows", new IRType(IRDataTypes.Int32), w.Int32(1));

                w.If(w.Compare(IRComparisonOperator.GreaterThan, groups, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension)), () =>
                {
                    w.Set(columns, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension));
                    w.Set(rows, w.Divide(w.Add(groups, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension - 1)), w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension)));
                });

                w.Com(list, ListSetComputeRootSignature, IRDataTypes.Void, rootSignature);

                w.Write(this.Descriptor, 4 * HLSLKernelLayout.CountConstant, count);
                w.Write(this.Descriptor, 4 * HLSLKernelLayout.RowConstant, w.Multiply(columns, w.Int32(groupSize)));

                for (int j = 0; j < inputs.Count; j++)
                {
                    w.Write(this.Descriptor, 4 * layout.LengthConstant(layout.Inputs[j]), w.Length(inputs[j]));
                }

                for (int s = 0; s < scalars.Count; s++)
                {
                    IRValueInstruction value = scalars[s].ValueType.DataType == IRDataTypes.Bool ? w.Convert(scalars[s], IRDataTypes.UInt32) : scalars[s];
                    w.Write(this.Descriptor, 4 * layout.ScalarConstant(layout.Scalars[s]), value);
                }

                w.Com(list, ListSetComputeRoot32BitConstants, IRDataTypes.Void, w.UInt32(0), w.UInt32(layout.ConstantCount), this.Descriptor, w.UInt32(0));
                w.Com(list, ListSetComputeRootUnorderedAccessView, IRDataTypes.Void, w.UInt32(1), w.Com(output, ResourceGetGPUVirtualAddress, IRDataTypes.UInt64));

                for (int j = 0; j < uploadLocals.Count; j++)
                {
                    w.Com(list, ListSetComputeRootShaderResourceView, IRDataTypes.Void, w.UInt32(2 + j), w.Com(uploadLocals[j], ResourceGetGPUVirtualAddress, IRDataTypes.UInt64));
                }

                w.Com(list, ListDispatch, IRDataTypes.Void, columns, rows, w.UInt32(1));

                w.Write(this.Descriptor, 64, w.Int32(0));
                w.Write(this.Descriptor, 68, w.Int32(0));
                w.Write(this.Descriptor, 72, output);
                w.Write(this.Descriptor, 80, w.Int32(-1));
                w.Write(this.Descriptor, 84, w.Int32(StateUnorderedAccess));
                w.Write(this.Descriptor, 88, w.Int32(StateCopySource));
                w.Com(list, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), w.Add(this.Descriptor, w.Int64(64)));

                w.Com(list, ListCopyResource, IRDataTypes.Void, readback, output);
                Check(w, w.Com(list, ListClose, IRDataTypes.Int32), "Could not record the GPU commands.");

                w.Write(this.Runtime.Scratch, 0, list);
                w.Com(this.Queue, QueueExecuteCommandLists, IRDataTypes.Void, w.UInt32(1), this.Runtime.Scratch);

                w.Set(this.FenceValue, w.Add(this.FenceValue, w.UInt64(1)));
                Check(w, w.Com(this.Queue, QueueSignal, IRDataTypes.Int32, this.Fence, this.FenceValue), "Could not signal the GPU fence.");

                w.Write(handle, HandleKind, w.Int32(KindPending));
                w.Write(handle, HandleInputCount, w.Int32(inputs.Count));
                w.Write(handle, HandleReadback, readback);
                w.Write(handle, HandleOutput, output);
                w.Write(handle, HandleAllocator, allocator);
                w.Write(handle, HandleList, list);
                w.Write(handle, HandleFenceValue, this.FenceValue);
                w.Write(handle, HandleInputs, uploads);
            }, () =>
            {
                w.Write(handle, HandleKind, w.Int32(KindCompleted));

                IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

                w.While(w.Compare(IRComparisonOperator.LessThan, i, count), () =>
                {
                    w.Invoke(kernel, [i, result, .. arguments]);
                    w.Set(i, w.Add(i, w.Int32(1)));
                });
            });

            w.Return(handle);

            return dispatcher;
        }

        IRFunction BuildPrepare(IRFunction kernel, byte[] shader, IRGlobal rootSignature, IRGlobal pipeline)
        {
            IRFunction prepare = this.Module.CreateFunction($"__gpu_prepare.{kernel.Name}", [new IRType(IRDataTypes.Bool)], [], isExport: false);
            X8664RuntimeWriter w = Writer(prepare);

            IRConstantBytes bytecode = this.Module.CreateConstantBytes(shader);

            w.If(w.Compare(IRComparisonOperator.EqualTo, pipeline, w.Pointer(0)), () =>
            {
                IRValueInstruction created = w.Com(this.Device, DeviceCreateRootSignature, IRDataTypes.Int32, w.UInt32(0), w.DataPointer(bytecode), w.UInt64(shader.Length), Identifier(w, Interfaces.RootSignature), this.Runtime.Scratch);

                w.If(w.Compare(IRComparisonOperator.LessThan, created, w.Int32(0)), () =>
                {
                    w.Return(w.Bool(false));
                });

                w.Set(rootSignature, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                w.Write(this.Descriptor, 0, rootSignature);
                w.Write(this.Descriptor, 8, w.DataPointer(bytecode));
                w.Write(this.Descriptor, 16, w.UInt64(shader.Length));
                w.Write(this.Descriptor, 24, w.Int32(0));
                w.Write(this.Descriptor, 28, w.Int32(0));
                w.Write(this.Descriptor, 32, w.Pointer(0));
                w.Write(this.Descriptor, 40, w.UInt64(0));
                w.Write(this.Descriptor, 48, w.Int32(0));
                w.Write(this.Descriptor, 52, w.Int32(0));

                IRValueInstruction pipelineCreated = w.Com(this.Device, DeviceCreateComputePipelineState, IRDataTypes.Int32, this.Descriptor, Identifier(w, Interfaces.PipelineState), this.Runtime.Scratch);

                w.If(w.Compare(IRComparisonOperator.LessThan, pipelineCreated, w.Int32(0)), () =>
                {
                    w.Return(w.Bool(false));
                });

                w.Set(pipeline, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            });

            w.Return(w.Bool(true));

            return prepare;
        }
    }
}
