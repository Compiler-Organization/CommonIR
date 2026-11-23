using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Translation;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
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
        const int HandleReadback = 8;
        const int HandleOutput = 16;
        const int HandleFenceValue = 40;
        const int HandleCount = 48;
        const int HandleStride = 52;
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

        const int ListCopyBufferRegion = 15;

        const int ListSetPipelineState = 25;

        const int RecordResource = 0;
        const int RecordCapacity = 8;
        const int RecordState = 16;
        const int RecordResourceState = 20;
        const int RecordEpoch = 24;
        const int RecordSize = 32;

        const int ResidentSame = 0;
        const int ResidentCpuNewer = 1;
        const int ResidentGpuNewer = 2;

        const int ReleaseNext = 0;
        const int ReleaseInstance = 8;
        const int ReleaseFence = 16;
        const int ReleaseSize = 24;

        const int DescriptorSize = 1024;
        const int DescriptorBarrier = 512;
        const int DescriptorUavBarrier = 576;
        const int DescriptorEmptyRange = 640;

        const int MaximumRecorded = 256;

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

        const int CommandListTypeDirect = 0;

        const int QueueWait = 15;

        const int ListCopyTextureRegion = 16;

        const int FactoryMakeWindowAssociation = 8;
        const int FactoryCreateSwapChainForHwnd = 15;
        const int FactoryEnumWarpAdapter = 27;

        const int SwapChainPresent = 8;
        const int SwapChainGetBuffer = 9;
        const int SwapChainGetCurrentBackBufferIndex = 36;
        const int SwapChainResizeBuffers = 13;

        const int DebugEnableDebugLayer = 3;

        const int UnknownQueryInterface = 0;

        const int StatePresent = 0x0;

        const int FormatB8G8R8A8 = 87;

        const int SurfaceSize = 256;
        const int SurfaceSwapChain = 0;
        const int SurfaceBuffers = 8;
        const int SurfaceWidth = 24;
        const int SurfaceHeight = 28;
        const int SurfaceFence = 32;
        const int SurfaceFenceValue = 40;
        const int SurfaceEvent = 48;
        const int SurfaceDescription = 64;
        const int SurfaceSource = 64;
        const int SurfaceDestination = 112;
        const int SurfaceBarrier = 160;
        const int SurfaceLists = 200;

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
            "1bc6ea02-ef36-464f-bf0c-21ca39e5168a",
            "2411e7e1-12ac-4ccf-bd14-9798e8534dc0",
            "94d99bdb-f1f8-4ab0-b236-7da0170edab1",
            "344488b7-6846-474b-b989-f027448245e0",
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
            Factory,
            Adapter,
            SwapChain,
            Debug,
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
        IRGlobal Factory { get; set; } = null!;
        IRGlobal DirectQueue { get; set; } = null!;
        IRGlobal List { get; set; } = null!;
        IRGlobal Allocator { get; set; } = null!;
        IRGlobal Recorded { get; set; } = null!;
        IRGlobal Releases { get; set; } = null!;
        IRGlobal FaultRecord { get; set; } = null!;
        IRGlobal FaultReadback { get; set; } = null!;
        IRGlobal FaultMapped { get; set; } = null!;

        IRConstantBytes Identifiers { get; set; } = null!;

        IRFunction Initialize { get; set; } = null!;
        IRFunction CreateBuffer { get; set; } = null!;
        IRFunction Release { get; set; } = null!;
        IRFunction CurrentFence { get; set; } = null!;
        IRFunction Defer { get; set; } = null!;
        IRFunction Collect { get; set; } = null!;
        IRFunction Open { get; set; } = null!;
        IRFunction Transition { get; set; } = null!;
        IRFunction Flush { get; set; } = null!;
        IRFunction WaitFor { get; set; } = null!;
        IRFunction Resident { get; set; } = null!;

        public IRFunction Sync { get; private set; } = null!;

        public IRFunction Forget { get; private set; } = null!;

        public IRFunction Await { get; private set; } = null!;

        public IRFunction Surface { get; private set; } = null!;

        public IRFunction Present { get; private set; } = null!;

        public IRFunction Resize { get; private set; } = null!;

        IRFunction CreateFactory { get; set; } = null!;

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
            this.Factory = Global("__gpu_factory", IRDataTypes.Pointer);
            this.DirectQueue = Global("__gpu_direct_queue", IRDataTypes.Pointer);
            this.List = Global("__gpu_list", IRDataTypes.Pointer);
            this.Allocator = Global("__gpu_allocator", IRDataTypes.Pointer);
            this.Recorded = Global("__gpu_recorded", IRDataTypes.Int32);
            this.Releases = Global("__gpu_releases", IRDataTypes.Pointer);
            this.FaultRecord = Global("__gpu_fault_record", IRDataTypes.Pointer);
            this.FaultReadback = Global("__gpu_fault_readback", IRDataTypes.Pointer);
            this.FaultMapped = Global("__gpu_fault_mapped", IRDataTypes.Pointer);

            this.Identifiers = this.Module.CreateConstantBytes(InterfaceIdentifiers.SelectMany(i => new Guid(i).ToByteArray()).ToArray());

            BuildRelease();
            BuildCreateFactory();
            BuildCreateBuffer();
            BuildInitialize();
            BuildCurrentFence();
            BuildDefer();
            BuildCollect();
            BuildOpen();
            BuildTransition();
            BuildFlush();
            BuildWaitFor();
            BuildResident();
            BuildSync();
            BuildForget();
            BuildAwait();
            BuildSurface();
            BuildPresent();
            BuildResize();

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
                w.Set(this.Descriptor, w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Runtime.Heap, w.UInt32(8), w.Int64(DescriptorSize)));

                IRLocal disabled = w.Local("disabled", new IRType(IRDataTypes.Int32), w.Native("kernel32.dll", "GetEnvironmentVariableA", IRDataTypes.Int32, w.DataPointer(w.Builder.BuildConstantString("COMMONIR_DISABLE_GPU")), this.Runtime.Scratch, w.UInt32(16)));

                w.If(w.Compare(IRComparisonOperator.EqualTo, disabled, w.Int32(0)), () =>
                {
                    IRLocal library = w.Local("library", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "LoadLibraryExA", IRDataTypes.Pointer, w.DataPointer(w.Builder.BuildConstantString("d3d12.dll")), w.Pointer(0), w.UInt32(0x800)));

                    w.If(w.Compare(IRComparisonOperator.NotEqualTo, library, w.Pointer(0)), () =>
                    {
                        IRLocal createDevice = w.Local("createDevice", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetProcAddress", IRDataTypes.Pointer, library, w.DataPointer(w.Builder.BuildConstantString("D3D12CreateDevice"))));

                        w.If(w.Compare(IRComparisonOperator.NotEqualTo, createDevice, w.Pointer(0)), () =>
                        {
                            IRLocal adapter = w.Local("adapter", new IRType(IRDataTypes.Pointer), w.Pointer(0));

                            w.If(w.Compare(IRComparisonOperator.NotEqualTo, Environment(w, "COMMONIR_GPU_DEBUG"), w.Int32(0)), () =>
                            {
                                IRLocal getDebug = w.Local("getDebug", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetProcAddress", IRDataTypes.Pointer, library, w.DataPointer(w.Builder.BuildConstantString("D3D12GetDebugInterface"))));

                                w.If(w.Compare(IRComparisonOperator.NotEqualTo, getDebug, w.Pointer(0)), () =>
                                {
                                    IRValueInstruction found = (IRValueInstruction)w.Builder.BuildCallIndirect(getDebug, new IRType(IRDataTypes.Int32), [Identifier(w, Interfaces.Debug), this.Runtime.Scratch]);

                                    w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, found, w.Int32(0)), () =>
                                    {
                                        IRLocal debug = w.Local("debug", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
                                        w.Com(debug, DebugEnableDebugLayer, IRDataTypes.Void);
                                        w.Invoke(this.Release, debug);
                                    });
                                });
                            });

                            w.If(w.Compare(IRComparisonOperator.NotEqualTo, Environment(w, "COMMONIR_GPU_WARP"), w.Int32(0)), () =>
                            {
                                w.If(w.Call(this.CreateFactory), () =>
                                {
                                    IRValueInstruction enumerated = w.Com(this.Factory, FactoryEnumWarpAdapter, IRDataTypes.Int32, Identifier(w, Interfaces.Adapter), this.Runtime.Scratch);

                                    w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, enumerated, w.Int32(0)), () =>
                                    {
                                        w.Set(adapter, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
                                    });
                                });
                            });

                            IRValueInstruction created = (IRValueInstruction)w.Builder.BuildCallIndirect(createDevice, new IRType(IRDataTypes.Int32), [adapter, w.Int32(FeatureLevel11), Identifier(w, Interfaces.Device), this.Runtime.Scratch]);

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

                                        w.Set(this.FaultRecord, Allocate(w, RecordSize));
                                        w.Write(this.FaultRecord, RecordResource, w.Call(this.CreateBuffer, w.Int32(HeapDefault), w.UInt64(4), w.Int32(FlagAllowUnorderedAccess), w.Int32(StateCommon)));
                                        w.Write(this.FaultRecord, RecordCapacity, w.UInt64(4));
                                        w.Set(this.FaultReadback, w.Call(this.CreateBuffer, w.Int32(HeapReadback), w.UInt64(4), w.Int32(0), w.Int32(StateCopyDestination)));
                                        Check(w, w.Com(this.FaultReadback, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not read results from the GPU.");
                                        w.Set(this.FaultMapped, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

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

        IRValueInstruction Environment(X8664RuntimeWriter w, string name)
        {
            return w.Native("kernel32.dll", "GetEnvironmentVariableA", IRDataTypes.Int32, w.DataPointer(w.Builder.BuildConstantString(name)), this.Runtime.Scratch, w.UInt32(16));
        }

        void BuildCreateFactory()
        {
            this.CreateFactory = this.Module.CreateFunction("__gpu_create_factory", [new IRType(IRDataTypes.Bool)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.CreateFactory);

            w.If(w.Compare(IRComparisonOperator.EqualTo, this.Factory, w.Pointer(0)), () =>
            {
                IRLocal library = w.Local("library", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "LoadLibraryExA", IRDataTypes.Pointer, w.DataPointer(w.Builder.BuildConstantString("dxgi.dll")), w.Pointer(0), w.UInt32(0x800)));

                w.If(w.Compare(IRComparisonOperator.NotEqualTo, library, w.Pointer(0)), () =>
                {
                    IRLocal create = w.Local("create", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "GetProcAddress", IRDataTypes.Pointer, library, w.DataPointer(w.Builder.BuildConstantString("CreateDXGIFactory2"))));

                    w.If(w.Compare(IRComparisonOperator.NotEqualTo, create, w.Pointer(0)), () =>
                    {
                        IRValueInstruction created = (IRValueInstruction)w.Builder.BuildCallIndirect(create, new IRType(IRDataTypes.Int32), [w.UInt32(0), Identifier(w, Interfaces.Factory), this.Runtime.Scratch]);

                        w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, created, w.Int32(0)), () =>
                        {
                            w.Set(this.Factory, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
                        });
                    });
                });
            });

            w.Return(w.Compare(IRComparisonOperator.NotEqualTo, this.Factory, w.Pointer(0)));
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

        void Fail(X8664RuntimeWriter w, string message)
        {
            w.Invoke(this.Runtime.Panic, w.Builder.BuildConstantString(message));
        }

        void BuildSurface()
        {
            this.Surface = this.Module.CreateFunction("__gpu_surface", [new IRType(IRDataTypes.Pointer)],
            [
                new IRLocal("window", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("width", IRDataTypes.Int32, isMutable: false),
                new IRLocal("height", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Surface);
            IRLocal window = w.Parameter(0);
            IRLocal width = w.Parameter(1);
            IRLocal height = w.Parameter(2);

            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Call(this.Initialize), w.Bool(false)), () => Fail(w, "Direct3D 12 is not available, so the window cannot be shown."));
            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Call(this.CreateFactory), w.Bool(false)), () => Fail(w, "DXGI is not available, so the window cannot be shown."));

            w.If(w.Compare(IRComparisonOperator.EqualTo, this.DirectQueue, w.Pointer(0)), () =>
            {
                w.Write(this.Descriptor, 0, w.Int32(CommandListTypeDirect));
                w.Write(this.Descriptor, 4, w.Int32(0));
                w.Write(this.Descriptor, 8, w.Int32(0));
                w.Write(this.Descriptor, 12, w.Int32(0));
                Check(w, w.Com(this.Device, DeviceCreateCommandQueue, IRDataTypes.Int32, this.Descriptor, Identifier(w, Interfaces.CommandQueue), this.Runtime.Scratch), "Could not create a GPU queue for the window.");
                w.Set(this.DirectQueue, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            });

            IRLocal surface = w.Local("surface", new IRType(IRDataTypes.Pointer), w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Runtime.Heap, w.UInt32(8), w.Int64(SurfaceSize)));
            w.If(w.Compare(IRComparisonOperator.EqualTo, surface, w.Pointer(0)), () => Fail(w, "Out of memory."));
            IRLocal description = w.Local("description", new IRType(IRDataTypes.Pointer), w.Add(surface, w.Int64(SurfaceDescription)));

            w.Write(description, 0, width);
            w.Write(description, 4, height);
            w.Write(description, 8, w.Int32(FormatB8G8R8A8));
            w.Write(description, 16, w.Int32(1));
            w.Write(description, 24, w.Int32(0x20));
            w.Write(description, 28, w.Int32(2));
            w.Write(description, 36, w.Int32(4));

            Check(w, w.Com(this.Factory, FactoryCreateSwapChainForHwnd, IRDataTypes.Int32, this.DirectQueue, window, description, w.Pointer(0), w.Pointer(0), this.Runtime.Scratch), "Could not create the window's swap chain.");
            IRLocal first = w.Local("first", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

            w.Com(this.Factory, FactoryMakeWindowAssociation, IRDataTypes.Int32, window, w.UInt32(2));

            Check(w, w.Com(first, UnknownQueryInterface, IRDataTypes.Int32, Identifier(w, Interfaces.SwapChain), this.Runtime.Scratch), "This version of Windows cannot show GPU frames in a window.");
            IRLocal chain = w.Local("chain", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            w.Invoke(this.Release, first);
            w.Write(surface, SurfaceSwapChain, chain);

            for (int buffer = 0; buffer < 2; buffer++)
            {
                Check(w, w.Com(chain, SwapChainGetBuffer, IRDataTypes.Int32, w.UInt32(buffer), Identifier(w, Interfaces.Resource), this.Runtime.Scratch), "Could not get the window's buffers.");
                w.Write(surface, SurfaceBuffers + 8 * buffer, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            }

            w.Write(surface, SurfaceWidth, width);
            w.Write(surface, SurfaceHeight, height);

            Check(w, w.Com(this.Device, DeviceCreateFence, IRDataTypes.Int32, w.UInt64(0), w.Int32(0), Identifier(w, Interfaces.Fence), this.Runtime.Scratch), "Could not create a GPU fence.");
            w.Write(surface, SurfaceFence, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            w.Write(surface, SurfaceEvent, w.Native("kernel32.dll", "CreateEventA", IRDataTypes.Pointer, w.Pointer(0), w.Int32(0), w.Int32(0), w.Pointer(0)));

            w.Return(surface);
        }

        void BuildPresent()
        {
            this.Present = this.Module.CreateFunction("__gpu_present", [new IRType(IRDataTypes.Void)],
            [
                new IRLocal("surface", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("frame", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("pitch", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Present);
            IRLocal surface = w.Parameter(0);
            IRLocal frame = w.Parameter(1);
            IRLocal pitch = w.Parameter(2);

            w.If(w.Compare(IRComparisonOperator.EqualTo, surface, w.Pointer(0)), () => Fail(w, "The window has no GPU surface."));
            w.If(w.Compare(IRComparisonOperator.EqualTo, frame, w.Pointer(0)), () => Fail(w, "The frame is null."));
            w.If(w.Compare(IRComparisonOperator.NotEqualTo, w.Read(frame, HandleKind, IRDataTypes.Int32), w.Int32(KindPending)), () => Fail(w, "A frame can only be shown once, before it is awaited."));

            IRLocal width = w.Local("width", new IRType(IRDataTypes.Int32), w.Read(surface, SurfaceWidth, IRDataTypes.Int32));
            IRLocal height = w.Local("height", new IRType(IRDataTypes.Int32), w.Read(surface, SurfaceHeight, IRDataTypes.Int32));

            w.If(w.Compare(IRComparisonOperator.LessThan, pitch, width), () => Fail(w, "The frame's rows are shorter than the window is wide."));
            w.If(w.Compare(IRComparisonOperator.NotEqualTo, w.Builder.BuildBitwise(IRBitwiseOperator.And, pitch, w.Int32(63)), w.Int32(0)), () => Fail(w, "The frame's rows must be a multiple of 64 pixels long."));
            w.If(w.Compare(IRComparisonOperator.GreaterThan, w.Multiply(w.Convert(pitch, IRDataTypes.Int64), w.Convert(height, IRDataTypes.Int64)), w.Convert(w.Read(frame, HandleCount, IRDataTypes.Int32), IRDataTypes.Int64)), () => Fail(w, "The frame has fewer pixels than the window."));

            IRLocal chain = w.Local("chain", new IRType(IRDataTypes.Pointer), w.Read(surface, SurfaceSwapChain, IRDataTypes.Pointer));
            IRLocal index = w.Local("index", new IRType(IRDataTypes.UInt32), w.Com(chain, SwapChainGetCurrentBackBufferIndex, IRDataTypes.UInt32));
            IRLocal target = w.Local("target", new IRType(IRDataTypes.Pointer), w.Read(surface, SurfaceBuffers, IRDataTypes.Pointer));

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, index, w.UInt32(0)), () =>
            {
                w.Set(target, w.Read(surface, SurfaceBuffers + 8, IRDataTypes.Pointer));
            });

            Check(w, w.Com(this.Device, DeviceCreateCommandAllocator, IRDataTypes.Int32, w.Int32(CommandListTypeDirect), Identifier(w, Interfaces.CommandAllocator), this.Runtime.Scratch), "Could not create a GPU command allocator.");
            IRLocal allocator = w.Local("allocator", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

            Check(w, w.Com(this.Device, DeviceCreateCommandList, IRDataTypes.Int32, w.UInt32(0), w.Int32(CommandListTypeDirect), allocator, w.Pointer(0), Identifier(w, Interfaces.GraphicsCommandList), this.Runtime.Scratch), "Could not create a GPU command list.");
            IRLocal list = w.Local("list", new IRType(IRDataTypes.Pointer), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

            IRLocal barrier = w.Local("barrier", new IRType(IRDataTypes.Pointer), w.Add(surface, w.Int64(SurfaceBarrier)));
            w.Write(barrier, 0, w.Int32(0));
            w.Write(barrier, 4, w.Int32(0));
            w.Write(barrier, 8, target);
            w.Write(barrier, 16, w.Int32(-1));
            w.Write(barrier, 20, w.Int32(StatePresent));
            w.Write(barrier, 24, w.Int32(StateCopyDestination));
            w.Com(list, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), barrier);

            IRLocal source = w.Local("source", new IRType(IRDataTypes.Pointer), w.Add(surface, w.Int64(SurfaceSource)));
            w.Write(source, 0, w.Read(frame, HandleOutput, IRDataTypes.Pointer));
            w.Write(source, 8, w.Int32(1));
            w.Write(source, 12, w.Int32(0));
            w.Write(source, 16, w.UInt64(0));
            w.Write(source, 24, w.Int32(FormatB8G8R8A8));
            w.Write(source, 28, width);
            w.Write(source, 32, height);
            w.Write(source, 36, w.Int32(1));
            w.Write(source, 40, w.Multiply(pitch, w.Int32(4)));

            IRLocal destination = w.Local("destination", new IRType(IRDataTypes.Pointer), w.Add(surface, w.Int64(SurfaceDestination)));
            w.Write(destination, 0, target);
            w.Write(destination, 8, w.Int32(0));
            w.Write(destination, 12, w.Int32(0));
            w.Write(destination, 16, w.Int32(0));

            w.Com(list, ListCopyTextureRegion, IRDataTypes.Void, destination, w.UInt32(0), w.UInt32(0), w.UInt32(0), source, w.Pointer(0));

            w.Write(barrier, 20, w.Int32(StateCopyDestination));
            w.Write(barrier, 24, w.Int32(StatePresent));
            w.Com(list, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), barrier);

            Check(w, w.Com(list, ListClose, IRDataTypes.Int32), "Could not record the GPU commands.");

            w.If(w.Compare(IRComparisonOperator.GreaterThan, w.Read(frame, HandleFenceValue, IRDataTypes.UInt64), this.FenceValue), () => w.Invoke(this.Flush));

            Check(w, w.Com(this.DirectQueue, QueueWait, IRDataTypes.Int32, this.Fence, w.Read(frame, HandleFenceValue, IRDataTypes.UInt64)), "Could not wait for the frame.");

            w.Write(surface, SurfaceLists, list);
            w.Com(this.DirectQueue, QueueExecuteCommandLists, IRDataTypes.Void, w.UInt32(1), w.Add(surface, w.Int64(SurfaceLists)));

            Check(w, w.Com(chain, SwapChainPresent, IRDataTypes.Int32, w.UInt32(1), w.UInt32(0)), "Could not show the frame. The GPU may have stopped responding.");

            IRLocal fence = w.Local("fence", new IRType(IRDataTypes.Pointer), w.Read(surface, SurfaceFence, IRDataTypes.Pointer));
            IRLocal value = w.Local("value", new IRType(IRDataTypes.UInt64), w.Add(w.Read(surface, SurfaceFenceValue, IRDataTypes.UInt64), w.UInt64(1)));
            w.Write(surface, SurfaceFenceValue, value);

            Check(w, w.Com(this.DirectQueue, QueueSignal, IRDataTypes.Int32, fence, value), "Could not signal the GPU fence.");

            w.If(w.Compare(IRComparisonOperator.LessThan, w.Com(fence, FenceGetCompletedValue, IRDataTypes.UInt64), value), () =>
            {
                IRLocal signal = w.Local("signal", new IRType(IRDataTypes.Pointer), w.Read(surface, SurfaceEvent, IRDataTypes.Pointer));
                Check(w, w.Com(fence, FenceSetEventOnCompletion, IRDataTypes.Int32, value, signal), "Could not wait for the GPU.");
                w.Native("kernel32.dll", "WaitForSingleObject", IRDataTypes.UInt32, signal, w.UInt32(0xFFFFFFFF));
            });

            w.Invoke(this.Release, list);
            w.Invoke(this.Release, allocator);
            w.Invoke(this.Await, frame);
            w.Return();
        }

        void BuildResize()
        {
            this.Resize = this.Module.CreateFunction("__gpu_resize", [new IRType(IRDataTypes.Void)],
            [
                new IRLocal("surface", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("width", IRDataTypes.Int32, isMutable: false),
                new IRLocal("height", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Resize);
            IRLocal surface = w.Parameter(0);
            IRLocal width = w.Parameter(1);
            IRLocal height = w.Parameter(2);

            w.If(w.Compare(IRComparisonOperator.EqualTo, surface, w.Pointer(0)), () => Fail(w, "The window has no GPU surface."));
            w.If(w.Compare(IRComparisonOperator.LessThanOrEqual, width, w.Int32(0)), () => w.Return());
            w.If(w.Compare(IRComparisonOperator.LessThanOrEqual, height, w.Int32(0)), () => w.Return());

            IRLocal chain = w.Local("chain", new IRType(IRDataTypes.Pointer), w.Read(surface, SurfaceSwapChain, IRDataTypes.Pointer));

            for (int buffer = 0; buffer < 2; buffer++)
            {
                w.Invoke(this.Release, w.Read(surface, SurfaceBuffers + 8 * buffer, IRDataTypes.Pointer));
                w.Write(surface, SurfaceBuffers + 8 * buffer, w.Pointer(0));
            }

            Check(w, w.Com(chain, SwapChainResizeBuffers, IRDataTypes.Int32, w.UInt32(2), width, height, w.Int32(FormatB8G8R8A8), w.UInt32(0)), "Could not resize the window's buffers.");

            for (int buffer = 0; buffer < 2; buffer++)
            {
                Check(w, w.Com(chain, SwapChainGetBuffer, IRDataTypes.Int32, w.UInt32(buffer), Identifier(w, Interfaces.Resource), this.Runtime.Scratch), "Could not get the window's buffers.");
                w.Write(surface, SurfaceBuffers + 8 * buffer, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
            }

            w.Write(surface, SurfaceWidth, width);
            w.Write(surface, SurfaceHeight, height);
            w.Return();
        }

        IRValueInstruction Pending(X8664RuntimeWriter w) => w.Add(this.FenceValue, w.UInt64(1));

        IRValueInstruction Allocate(X8664RuntimeWriter w, int bytes) => w.Native("kernel32.dll", "HeapAlloc", IRDataTypes.Pointer, this.Runtime.Heap, w.UInt32(8), w.Int64(bytes));

        void Deallocate(X8664RuntimeWriter w, IRValueInstruction pointer) => w.Native("kernel32.dll", "HeapFree", IRDataTypes.Int32, this.Runtime.Heap, w.UInt32(0), pointer);

        void BuildCurrentFence()
        {
            this.CurrentFence = this.Module.CreateFunction("__gpu_current_fence", [new IRType(IRDataTypes.UInt64)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.CurrentFence);

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, this.List, w.Pointer(0)), () => w.Return(Pending(w)));
            w.Return(this.FenceValue);
        }

        void BuildDefer()
        {
            this.Defer = this.Module.CreateFunction("__gpu_defer", [new IRType(IRDataTypes.Void)],
            [
                new IRLocal("instance", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("fence", IRDataTypes.UInt64, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Defer);
            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Parameter(0), w.Pointer(0)), () => w.Return());

            IRLocal node = w.Local("node", new IRType(IRDataTypes.Pointer), Allocate(w, ReleaseSize));
            w.Write(node, ReleaseNext, this.Releases);
            w.Write(node, ReleaseInstance, w.Parameter(0));
            w.Write(node, ReleaseFence, w.Parameter(1));
            w.Set(this.Releases, node);
            w.Return();
        }

        void BuildCollect()
        {
            this.Collect = this.Module.CreateFunction("__gpu_collect", [new IRType(IRDataTypes.Void)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.Collect);

            IRLocal completed = w.Local("completed", new IRType(IRDataTypes.UInt64), w.Com(this.Fence, FenceGetCompletedValue, IRDataTypes.UInt64));
            IRLocal current = w.Local("current", new IRType(IRDataTypes.Pointer), this.Releases);
            IRLocal kept = w.Local("kept", new IRType(IRDataTypes.Pointer), w.Pointer(0));
            IRLocal next = w.Local("next", new IRType(IRDataTypes.Pointer), w.Pointer(0));

            w.While(w.Compare(IRComparisonOperator.NotEqualTo, current, w.Pointer(0)), () =>
            {
                w.Set(next, w.Read(current, ReleaseNext, IRDataTypes.Pointer));

                w.If(w.Compare(IRComparisonOperator.LessThanOrEqual, w.Read(current, ReleaseFence, IRDataTypes.UInt64), completed), () =>
                {
                    w.Invoke(this.Release, w.Read(current, ReleaseInstance, IRDataTypes.Pointer));
                    Deallocate(w, current);
                }, () =>
                {
                    w.Write(current, ReleaseNext, kept);
                    w.Set(kept, current);
                });

                w.Set(current, next);
            });

            w.Set(this.Releases, kept);
            w.Return();
        }

        void BuildOpen()
        {
            this.Open = this.Module.CreateFunction("__gpu_open", [new IRType(IRDataTypes.Void)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.Open);

            w.If(w.Compare(IRComparisonOperator.EqualTo, this.List, w.Pointer(0)), () =>
            {
                Check(w, w.Com(this.Device, DeviceCreateCommandAllocator, IRDataTypes.Int32, w.Int32(CommandListTypeCompute), Identifier(w, Interfaces.CommandAllocator), this.Runtime.Scratch), "Could not create a GPU command allocator.");
                w.Set(this.Allocator, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));

                Check(w, w.Com(this.Device, DeviceCreateCommandList, IRDataTypes.Int32, w.UInt32(0), w.Int32(CommandListTypeCompute), this.Allocator, w.Pointer(0), Identifier(w, Interfaces.GraphicsCommandList), this.Runtime.Scratch), "Could not create a GPU command list.");
                w.Set(this.List, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer));
                w.Set(this.Recorded, w.Int32(0));
            });

            w.Return();
        }

        void BuildTransition()
        {
            this.Transition = this.Module.CreateFunction("__gpu_transition", [new IRType(IRDataTypes.Void)],
            [
                new IRLocal("record", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("state", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Transition);
            IRLocal record = w.Parameter(0);
            IRLocal state = w.Parameter(1);

            IRLocal current = w.Local("current", new IRType(IRDataTypes.Int32), w.Int32(StateCommon));

            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Read(record, RecordEpoch, IRDataTypes.UInt64), Pending(w)), () =>
            {
                w.Set(current, w.Read(record, RecordResourceState, IRDataTypes.Int32));
            });

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, current, state), () =>
            {
                IRLocal barrier = w.Local("barrier", new IRType(IRDataTypes.Pointer), w.Add(this.Descriptor, w.Int64(DescriptorBarrier)));
                w.Write(barrier, 0, w.Int32(0));
                w.Write(barrier, 4, w.Int32(0));
                w.Write(barrier, 8, w.Read(record, RecordResource, IRDataTypes.Pointer));
                w.Write(barrier, 16, w.Int32(-1));
                w.Write(barrier, 20, current);
                w.Write(barrier, 24, state);
                w.Com(this.List, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), barrier);
            });

            w.Write(record, RecordResourceState, state);
            w.Write(record, RecordEpoch, Pending(w));
            w.Return();
        }

        void BuildFlush()
        {
            this.Flush = this.Module.CreateFunction("__gpu_flush", [new IRType(IRDataTypes.Void)], [], isExport: false);
            X8664RuntimeWriter w = Writer(this.Flush);

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, this.List, w.Pointer(0)), () =>
            {
                w.Invoke(this.Transition, this.FaultRecord, w.Int32(StateCopySource));
                w.Com(this.List, ListCopyBufferRegion, IRDataTypes.Void, this.FaultReadback, w.UInt64(0), w.Read(this.FaultRecord, RecordResource, IRDataTypes.Pointer), w.UInt64(0), w.UInt64(4));

                Check(w, w.Com(this.List, ListClose, IRDataTypes.Int32), "Could not record the GPU commands.");

                w.Write(this.Runtime.Scratch, 0, this.List);
                w.Com(this.Queue, QueueExecuteCommandLists, IRDataTypes.Void, w.UInt32(1), this.Runtime.Scratch);

                w.Set(this.FenceValue, w.Add(this.FenceValue, w.UInt64(1)));
                Check(w, w.Com(this.Queue, QueueSignal, IRDataTypes.Int32, this.Fence, this.FenceValue), "Could not signal the GPU fence.");

                w.Invoke(this.Defer, this.List, this.FenceValue);
                w.Invoke(this.Defer, this.Allocator, this.FenceValue);
                w.Set(this.List, w.Pointer(0));
                w.Set(this.Allocator, w.Pointer(0));
                w.Set(this.Recorded, w.Int32(0));
            });

            w.Return();
        }

        void BuildWaitFor()
        {
            this.WaitFor = this.Module.CreateFunction("__gpu_wait", [new IRType(IRDataTypes.Void)], [new IRLocal("value", IRDataTypes.UInt64, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.WaitFor);
            IRLocal value = w.Parameter(0);

            w.If(w.Compare(IRComparisonOperator.GreaterThan, value, this.FenceValue), () => w.Invoke(this.Flush));

            w.If(w.Compare(IRComparisonOperator.LessThan, w.Com(this.Fence, FenceGetCompletedValue, IRDataTypes.UInt64), value), () =>
            {
                Check(w, w.Com(this.Fence, FenceSetEventOnCompletion, IRDataTypes.Int32, value, this.Event), "Could not wait for the GPU.");
                w.Native("kernel32.dll", "WaitForSingleObject", IRDataTypes.UInt32, this.Event, w.UInt32(0xFFFFFFFF));
            });

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, w.Read(this.FaultMapped, 0, IRDataTypes.UInt32), w.UInt32(0)), () =>
            {
                w.Invoke(this.Runtime.Panic, w.Builder.BuildConstantString(Emission.X8664FunctionEmitter.IndexOutOfRangeMessage));
            });

            w.Invoke(this.Collect);
            w.Return();
        }

        void BuildResident()
        {
            this.Resident = this.Module.CreateFunction("__gpu_resident", [new IRType(IRDataTypes.Pointer)],
            [
                new IRLocal("record", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("data", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("length", IRDataTypes.Int32, isMutable: false),
                new IRLocal("stride", IRDataTypes.Int32, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Resident);
            IRLocal data = w.Parameter(1);

            IRLocal record = w.Local("current", new IRType(IRDataTypes.Pointer), w.Parameter(0));
            IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(w.Parameter(2), IRDataTypes.Int64), w.Convert(w.Parameter(3), IRDataTypes.Int64)), IRDataTypes.UInt64));
            IRLocal allocation = w.Local("allocation", new IRType(IRDataTypes.UInt64), bytes);

            w.If(w.Compare(IRComparisonOperator.EqualTo, allocation, w.UInt64(0)), () => w.Set(allocation, w.UInt64(4)));

            w.If(w.Compare(IRComparisonOperator.EqualTo, record, w.Pointer(0)), () =>
            {
                w.Set(record, Allocate(w, RecordSize));
                w.Write(record, RecordState, w.Int32(ResidentCpuNewer));
            });

            w.If(w.Compare(IRComparisonOperator.LessThan, w.Read(record, RecordCapacity, IRDataTypes.UInt64), allocation), () =>
            {
                w.Invoke(this.Defer, w.Read(record, RecordResource, IRDataTypes.Pointer), w.Call(this.CurrentFence));
                w.Write(record, RecordResource, w.Call(this.CreateBuffer, w.Int32(HeapDefault), allocation, w.Int32(FlagAllowUnorderedAccess), w.Int32(StateCommon)));
                w.Write(record, RecordCapacity, allocation);
                w.Write(record, RecordEpoch, w.UInt64(0));
                w.Write(record, RecordState, w.Int32(ResidentCpuNewer));
            });

            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Read(record, RecordState, IRDataTypes.Int32), w.Int32(ResidentCpuNewer)), () =>
            {
                w.If(w.Compare(IRComparisonOperator.GreaterThan, bytes, w.UInt64(0)), () =>
                {
                    IRLocal staging = w.Local("staging", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapUpload), bytes, w.Int32(0), w.Int32(StateGenericRead)));
                    Check(w, w.Com(staging, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not upload data to the GPU.");
                    w.Native("kernel32.dll", "RtlMoveMemory", IRDataTypes.Void, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer), data, bytes);
                    w.Com(staging, ResourceUnmap, IRDataTypes.Void, w.UInt32(0), w.Pointer(0));

                    w.Invoke(this.Open);
                    w.Invoke(this.Transition, record, w.Int32(StateCopyDestination));
                    w.Com(this.List, ListCopyBufferRegion, IRDataTypes.Void, w.Read(record, RecordResource, IRDataTypes.Pointer), w.UInt64(0), staging, w.UInt64(0), bytes);
                    w.Invoke(this.Defer, staging, Pending(w));
                });

                w.Write(record, RecordState, w.Int32(ResidentSame));
            });

            w.Return(record);
        }

        void BuildSync()
        {
            this.Sync = this.Module.CreateFunction("__gpu_sync", [new IRType(IRDataTypes.Void)],
            [
                new IRLocal("record", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("data", IRDataTypes.Pointer, isMutable: false),
                new IRLocal("length", IRDataTypes.Int32, isMutable: false),
                new IRLocal("stride", IRDataTypes.Int32, isMutable: false),
                new IRLocal("write", IRDataTypes.Bool, isMutable: false),
            ], isExport: false);

            X8664RuntimeWriter w = Writer(this.Sync);
            IRLocal record = w.Parameter(0);
            IRLocal data = w.Parameter(1);

            w.If(w.Compare(IRComparisonOperator.EqualTo, record, w.Pointer(0)), () => w.Return());

            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Read(record, RecordState, IRDataTypes.Int32), w.Int32(ResidentGpuNewer)), () =>
            {
                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(w.Parameter(2), IRDataTypes.Int64), w.Convert(w.Parameter(3), IRDataTypes.Int64)), IRDataTypes.UInt64));

                w.If(w.Compare(IRComparisonOperator.GreaterThan, bytes, w.Read(record, RecordCapacity, IRDataTypes.UInt64)), () =>
                {
                    w.Set(bytes, w.Read(record, RecordCapacity, IRDataTypes.UInt64));
                });

                w.If(w.Compare(IRComparisonOperator.GreaterThan, bytes, w.UInt64(0)), () =>
                {
                    IRLocal readback = w.Local("readback", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapReadback), bytes, w.Int32(0), w.Int32(StateCopyDestination)));

                    w.Invoke(this.Open);
                    w.Invoke(this.Transition, record, w.Int32(StateCopySource));
                    w.Com(this.List, ListCopyBufferRegion, IRDataTypes.Void, readback, w.UInt64(0), w.Read(record, RecordResource, IRDataTypes.Pointer), w.UInt64(0), bytes);
                    w.Invoke(this.Flush);
                    w.Invoke(this.WaitFor, this.FenceValue);

                    Check(w, w.Com(readback, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not read results from the GPU.");
                    w.Native("kernel32.dll", "RtlMoveMemory", IRDataTypes.Void, data, w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer), bytes);
                    w.Write(this.Descriptor, DescriptorEmptyRange, w.UInt64(0));
                    w.Write(this.Descriptor, DescriptorEmptyRange + 8, w.UInt64(0));
                    w.Com(readback, ResourceUnmap, IRDataTypes.Void, w.UInt32(0), w.Add(this.Descriptor, w.Int64(DescriptorEmptyRange)));
                    w.Invoke(this.Release, readback);
                });

                w.Write(record, RecordState, w.Int32(ResidentSame));
            });

            w.If(w.Parameter(4), () => w.Write(record, RecordState, w.Int32(ResidentCpuNewer)));
            w.Return();
        }

        void BuildForget()
        {
            this.Forget = this.Module.CreateFunction("__gpu_forget", [new IRType(IRDataTypes.Void)], [new IRLocal("record", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Forget);
            IRLocal record = w.Parameter(0);

            w.If(w.Compare(IRComparisonOperator.NotEqualTo, record, w.Pointer(0)), () =>
            {
                w.Invoke(this.Defer, w.Read(record, RecordResource, IRDataTypes.Pointer), w.Call(this.CurrentFence));
                Deallocate(w, record);
            });

            w.Return();
        }

        void BuildAwait()
        {
            IRType arrayType = new IRType(IRDataTypes.Array, this.Module.CreateArraySchema(new IRType(IRDataTypes.UInt8)));

            this.Await = this.Module.CreateFunction("__gpu_await", [arrayType], [new IRLocal("handle", IRDataTypes.Pointer, isMutable: false)], isExport: false);
            X8664RuntimeWriter w = Writer(this.Await);
            IRLocal handle = w.Parameter(0);
            w.If(w.Compare(IRComparisonOperator.EqualTo, w.Read(handle, HandleKind, IRDataTypes.Int32), w.Int32(KindPending)), () =>
            {
                w.Invoke(this.WaitFor, w.Read(handle, HandleFenceValue, IRDataTypes.UInt64));

                IRLocal readback = w.Local("readback", new IRType(IRDataTypes.Pointer), w.Read(handle, HandleReadback, IRDataTypes.Pointer));
                IRLocal result = w.Local("result", arrayType, w.Read(handle, HandleResult, arrayType));
                IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(w.Read(handle, HandleCount, IRDataTypes.Int32), IRDataTypes.Int64), w.Convert(w.Read(handle, HandleStride, IRDataTypes.Int32), IRDataTypes.Int64)), IRDataTypes.UInt64));

                Check(w, w.Com(readback, ResourceMap, IRDataTypes.Int32, w.UInt32(0), w.Pointer(0), this.Runtime.Scratch), "Could not read the results from the GPU.");
                w.Native("kernel32.dll", "RtlMoveMemory", IRDataTypes.Void, w.DataPointer(result), w.Read(this.Runtime.Scratch, 0, IRDataTypes.Pointer), bytes);

                w.Write(this.Descriptor, DescriptorEmptyRange, w.UInt64(0));
                w.Write(this.Descriptor, DescriptorEmptyRange + 8, w.UInt64(0));
                w.Com(readback, ResourceUnmap, IRDataTypes.Void, w.UInt32(0), w.Add(this.Descriptor, w.Int64(DescriptorEmptyRange)));

                w.Invoke(this.Release, readback);
                w.Invoke(this.Release, w.Read(handle, HandleOutput, IRDataTypes.Pointer));
                w.Write(handle, HandleKind, w.Int32(KindCompleted));
            });

            w.Return(w.Read(handle, HandleResult, arrayType));
        }

        void TransitionResource(X8664RuntimeWriter w, IRValueInstruction resource, int before, int after)
        {
            IRLocal barrier = w.Local($"barrier{this.Barriers++}", new IRType(IRDataTypes.Pointer), w.Add(this.Descriptor, w.Int64(DescriptorBarrier)));
            w.Write(barrier, 0, w.Int32(0));
            w.Write(barrier, 4, w.Int32(0));
            w.Write(barrier, 8, resource);
            w.Write(barrier, 16, w.Int32(-1));
            w.Write(barrier, 20, w.Int32(before));
            w.Write(barrier, 24, w.Int32(after));
            w.Com(this.List, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), barrier);
        }

        int Barriers { get; set; }

        static List<int>? ResidentOffsets(IRFunction kernel)
        {
            List<int>? offsets = null;

            foreach (IRDispatch dispatch in kernel.References.OfType<IRDispatch>().Where(d => d.Residents.Count > 0))
            {
                List<int> these = dispatch.ResidentSlots.Select(Emission.X8664Layout.OffsetOf).ToList();

                if (offsets != null && !offsets.SequenceEqual(these))
                {
                    throw ErrorHandler.Create($"The dispatches of kernel '{kernel.Name}' keep their arrays' GPU copies in different places.");
                }

                offsets = these;
            }

            return offsets;
        }

        IRFunction BuildDispatcher(IRFunction kernel, byte[] shader)
        {
            HLSLKernelLayout layout = new HLSLKernelLayout(kernel);
            int stride = layout.HasOutput ? HLSLKernelLayout.StrideOf(layout.OutputElementType!, kernel.Name) : 0;
            int groupSize = this.Configuration.Shaders.ThreadGroupSizes.X;
            List<int>? offsets = ResidentOffsets(kernel);

            IRGlobal rootSignature = Global($"__gpu_root_signature.{kernel.Name}", IRDataTypes.Pointer);
            IRGlobal pipeline = Global($"__gpu_pipeline.{kernel.Name}", IRDataTypes.Pointer);

            IRFunction prepare = BuildPrepare(kernel, shader, rootSignature, pipeline);

            List<IRLocal> parameters =
            [
                new IRLocal("count", IRDataTypes.Int32, isMutable: false),
                .. kernel.KernelParameters.Select(p => new IRLocal(p.Name, p.ValueType, isMutable: false)),
                .. layout.Inputs.Select((_, j) => new IRLocal($"resident{j}", IRDataTypes.Pointer, isMutable: false)),
            ];

            IRFunction dispatcher = this.Module.CreateFunction($"__gpu_dispatch.{kernel.Name}", [new IRType(IRDataTypes.Pointer)], parameters, isExport: false);

            X8664RuntimeWriter w = Writer(dispatcher);

            IRLocal count = w.Parameter(0);
            List<IRLocal> arguments = dispatcher.Parameters.Skip(1).Take(kernel.KernelParameters.Count).ToList();
            List<IRLocal> inputs = layout.Inputs.Select(input => arguments[kernel.KernelParameters.IndexOf(input)]).ToList();
            List<IRLocal> scalars = layout.Scalars.Select(scalar => arguments[kernel.KernelParameters.IndexOf(scalar)]).ToList();
            List<IRLocal> residents = dispatcher.Parameters.Skip(1 + kernel.KernelParameters.Count).ToList();

            IRLocal handle = w.Local("handle", new IRType(IRDataTypes.Pointer), w.Pointer(0));
            IRLocal? result = null;

            if (layout.HasOutput)
            {
                IRType outputType = kernel.KernelOutput.ValueType;
                w.Set(handle, w.Call(this.Runtime.Malloc, w.Int64(HandleSize)));
                result = w.Local("result", outputType, w.Builder.BuildInstantiateArray((IRArraySchema)outputType.UserObject!, count));

                w.Write(handle, HandleCount, count);
                w.Write(handle, HandleStride, w.Int32(stride));
                w.Write(handle, HandleResult, result);
            }

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
                w.Invoke(this.Open);

                List<IRLocal> records = new List<IRLocal>();

                for (int j = 0; j < inputs.Count; j++)
                {
                    int input = j;
                    IRLocal record = w.Local($"record{j}", new IRType(IRDataTypes.Pointer), w.Pointer(0));
                    records.Add(record);

                    if (offsets != null)
                    {
                        w.If(w.Compare(IRComparisonOperator.NotEqualTo, residents[input], w.Pointer(0)), () =>
                        {
                            w.Set(record, w.Read(residents[input], offsets[input], IRDataTypes.Pointer));
                        });
                    }

                    w.Set(record, w.Call(this.Resident, record, w.DataPointer(inputs[input]), w.Length(inputs[input]), w.Int32(4)));

                    if (offsets != null)
                    {
                        w.If(w.Compare(IRComparisonOperator.NotEqualTo, residents[input], w.Pointer(0)), () =>
                        {
                            w.Write(residents[input], offsets[input], record);
                        });
                    }

                    w.Invoke(this.Transition, record, w.Int32(StateUnorderedAccess));
                }

                w.Invoke(this.Transition, this.FaultRecord, w.Int32(StateUnorderedAccess));

                IRLocal? output = null;
                IRLocal? readback = null;

                if (layout.HasOutput)
                {
                    IRLocal bytes = w.Local("bytes", new IRType(IRDataTypes.UInt64), w.Convert(w.Multiply(w.Convert(count, IRDataTypes.Int64), w.Int64(stride)), IRDataTypes.UInt64));
                    output = w.Local("output", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapDefault), bytes, w.Int32(FlagAllowUnorderedAccess), w.Int32(StateCommon)));
                    readback = w.Local("readback", new IRType(IRDataTypes.Pointer), w.Call(this.CreateBuffer, w.Int32(HeapReadback), bytes, w.Int32(0), w.Int32(StateCopyDestination)));
                    TransitionResource(w, output, StateCommon, StateUnorderedAccess);
                }

                IRLocal groups = w.Local("groups", new IRType(IRDataTypes.Int32), w.Divide(w.Add(count, w.Int32(groupSize - 1)), w.Int32(groupSize)));
                IRLocal columns = w.Local("columns", new IRType(IRDataTypes.Int32), groups);
                IRLocal rows = w.Local("rows", new IRType(IRDataTypes.Int32), w.Int32(1));

                w.If(w.Compare(IRComparisonOperator.GreaterThan, groups, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension)), () =>
                {
                    w.Set(columns, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension));
                    w.Set(rows, w.Divide(w.Add(groups, w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension - 1)), w.Int32(HLSLKernelLayout.MaximumGroupsPerDimension)));
                });

                w.Com(this.List, ListSetPipelineState, IRDataTypes.Void, pipeline);
                w.Com(this.List, ListSetComputeRootSignature, IRDataTypes.Void, rootSignature);

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

                w.Com(this.List, ListSetComputeRoot32BitConstants, IRDataTypes.Void, w.UInt32(0), w.UInt32(layout.ConstantCount), this.Descriptor, w.UInt32(0));
                w.Com(this.List, ListSetComputeRootUnorderedAccessView, IRDataTypes.Void, w.UInt32(1), w.Com(w.Read(this.FaultRecord, RecordResource, IRDataTypes.Pointer), ResourceGetGPUVirtualAddress, IRDataTypes.UInt64));

                if (output != null)
                {
                    w.Com(this.List, ListSetComputeRootUnorderedAccessView, IRDataTypes.Void, w.UInt32(1 + layout.OutputRegister), w.Com(output, ResourceGetGPUVirtualAddress, IRDataTypes.UInt64));
                }

                for (int j = 0; j < records.Count; j++)
                {
                    w.Com(this.List, ListSetComputeRootUnorderedAccessView, IRDataTypes.Void, w.UInt32(1 + layout.InputRegister(j)), w.Com(w.Read(records[j], RecordResource, IRDataTypes.Pointer), ResourceGetGPUVirtualAddress, IRDataTypes.UInt64));
                }

                IRLocal uav = w.Local("uav", new IRType(IRDataTypes.Pointer), w.Add(this.Descriptor, w.Int64(DescriptorUavBarrier)));
                w.Write(uav, 0, w.Int32(2));
                w.Write(uav, 4, w.Int32(0));
                w.Write(uav, 8, w.Pointer(0));
                w.Com(this.List, ListResourceBarrier, IRDataTypes.Void, w.UInt32(1), uav);

                w.Com(this.List, ListDispatch, IRDataTypes.Void, columns, rows, w.UInt32(1));

                for (int j = 0; j < records.Count; j++)
                {
                    int input = j;

                    if (layout.Written.Contains(layout.Inputs[j]))
                    {
                        w.Write(records[j], RecordState, w.Int32(ResidentGpuNewer));
                    }

                    if (offsets == null)
                    {
                        w.Invoke(this.Forget, records[input]);
                    }
                    else
                    {
                        w.If(w.Compare(IRComparisonOperator.EqualTo, residents[input], w.Pointer(0)), () => w.Invoke(this.Forget, records[input]));
                    }
                }

                if (output != null)
                {
                    TransitionResource(w, output, StateUnorderedAccess, StateCopySource);
                    w.Com(this.List, ListCopyResource, IRDataTypes.Void, readback!, output);

                    w.Write(handle, HandleKind, w.Int32(KindPending));
                    w.Write(handle, HandleReadback, readback!);
                    w.Write(handle, HandleOutput, output);
                    w.Write(handle, HandleFenceValue, Pending(w));
                }

                w.Set(this.Recorded, w.Add(this.Recorded, w.Int32(1)));
                w.If(w.Compare(IRComparisonOperator.GreaterThanOrEqual, this.Recorded, w.Int32(MaximumRecorded)), () => w.Invoke(this.Flush));
            }, () =>
            {
                if (layout.HasOutput)
                {
                    w.Write(handle, HandleKind, w.Int32(KindCompleted));
                }

                if (offsets != null)
                {
                    for (int j = 0; j < inputs.Count; j++)
                    {
                        int input = j;
                        bool written = layout.Written.Contains(layout.Inputs[j]);

                        w.If(w.Compare(IRComparisonOperator.NotEqualTo, residents[input], w.Pointer(0)), () =>
                        {
                            w.Invoke(this.Sync, w.Read(residents[input], offsets[input], IRDataTypes.Pointer), w.DataPointer(inputs[input]), w.Length(inputs[input]), w.Int32(4), w.Bool(written));
                        });
                    }
                }

                IRLocal i = w.Local("i", new IRType(IRDataTypes.Int32), w.Int32(0));

                w.While(w.Compare(IRComparisonOperator.LessThan, i, count), () =>
                {
                    w.Invoke(kernel, result != null ? [i, result, .. arguments] : [i, .. arguments]);
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
