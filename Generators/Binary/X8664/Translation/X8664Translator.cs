using CommonIR.Errors;
using CommonIR.Generators.Binary.X8664.Emission;
using CommonIR.Generators.Binary.X8664.Model;
using CommonIR.Generators.Binary.X8664.Runtime;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Translation
{
    internal class X8664Translator
    {
        IRModule Module { get; set; }

        CommonIRX8664Configuration Configuration { get; set; }

        public List<SourceFile> ShaderSources { get; private set; } = new List<SourceFile>();

        public X8664Translator(IRModule module, CommonIRX8664Configuration configuration)
        {
            this.Module = module;
            this.Configuration = configuration;
        }

        public byte[] Translate()
        {
            X8664Context context = new X8664Context { OptimizeLoops = this.Configuration.OptimizeLoops };

            X8664RuntimeBuilder runtime = new X8664RuntimeBuilder(this.Module, this.Configuration);
            runtime.Build(context);
            this.ShaderSources = runtime.ShaderSources;

            foreach (IRFunction function in this.Module.Functions)
            {
                context.Functions[function] = new X8664Symbol(function.Name, X8664Sections.Text);
            }

            X8664ImportTable imports = new X8664ImportTable();

            foreach (IRFunctionImport functionImport in this.Module.FunctionImports)
            {
                if (!runtime.SystemImports.ContainsKey(functionImport) && !context.RuntimeImports.ContainsKey(functionImport))
                {
                    context.Imports[functionImport] = imports.Add(functionImport);
                }
            }

            int dataSize = 0;

            int threadDataSize = 0;

            foreach (IRGlobal global in this.Module.Globals)
            {
                if (global.IsThreadLocal)
                {
                    context.ThreadLocals[global] = threadDataSize;
                    threadDataSize += 16;
                    continue;
                }

                context.Globals[global] = new X8664Symbol(global.Name ?? "global", X8664Sections.Data, dataSize);
                dataSize += 16;
            }

            int threadDirectory = X8664PortableExecutable.AlignUp(dataSize, ThreadDirectoryAlignment);

            if (threadDataSize > 0)
            {
                context.ThreadIndex = new X8664Symbol("__tls_index", X8664Sections.Data, threadDirectory + ThreadIndexOffset);
                dataSize = threadDirectory + ThreadTemplateOffset + threadDataSize;
            }

            foreach ((IRFunctionImport functionImport, IRGlobal slot) in runtime.SystemImports)
            {
                context.Imports[functionImport] = context.Globals[slot];
            }

            imports.Layout(dataSize);

            foreach (IRObject constant in this.Module.Objects)
            {
                switch (constant)
                {
                    case IRConstantString constantString:
                        context.Constants[constantString] = context.AddReadOnly("string", [.. Encoding.UTF8.GetBytes(constantString.Value), 0], 1);
                        break;

                    case IRConstantBytes constantBytes:
                        context.Constants[constantBytes] = context.AddReadOnly("bytes", constantBytes.Value, 16);
                        break;
                }
            }

            X8664FunctionEmitter.EmitProbe(context);

            foreach (IRFunction function in this.Module.Functions)
            {
                try
                {
                    new X8664FunctionEmitter(context, function).Emit();
                }
                catch (Exception ex)
                {
                    throw ErrorHandler.Create($"Could not translate '{function.Name}' to x86-64: {ex.Message}");
                }
            }

            int textSize = context.Assembler.Code.Count;
            int textAddress = X8664PortableExecutable.TextAddress;
            int readOnlyAddress = X8664PortableExecutable.ReadOnlyDataAddress(textSize);

            List<(X8664UnwindEntry Entry, X8664Symbol Information)> unwindInformation = context.UnwindEntries
                .OrderBy(e => e.Start)
                .Select(e => (e, context.AddReadOnly("unwind", e.Serialize(), 4)))
                .ToList();

            List<byte> exceptionTable = new List<byte>();

            foreach ((X8664UnwindEntry entry, X8664Symbol information) in unwindInformation)
            {
                exceptionTable.AddRange(BitConverter.GetBytes(textAddress + entry.Start));
                exceptionTable.AddRange(BitConverter.GetBytes(textAddress + entry.End));
                exceptionTable.AddRange(BitConverter.GetBytes(readOnlyAddress + information.Offset));
            }

            X8664Symbol exceptionDirectory = context.AddReadOnly("pdata", exceptionTable.ToArray(), 4);
            X8664Symbol? relocations = threadDataSize > 0 ? context.AddReadOnly("reloc", new byte[RelocationBlockSize], 4) : null;

            int readOnlySize = context.ReadOnlyData.Count;
            int dataAddress = X8664PortableExecutable.DataAddress(textSize, readOnlySize);

            int AddressOf(X8664Symbol symbol)
            {
                if (!symbol.IsBound)
                {
                    throw ErrorHandler.Create($"The symbol '{symbol.Name}' was never defined.");
                }

                return symbol.Section switch
                {
                    X8664Sections.Text => textAddress,
                    X8664Sections.ReadOnlyData => readOnlyAddress,
                    _ => dataAddress
                } + symbol.Offset;
            }

            foreach (X8664Fixup fixup in context.Assembler.Fixups)
            {
                int target = AddressOf(fixup.Target) + fixup.Addend;
                int next = textAddress + fixup.Position + 4;

                context.Assembler.Patch32(fixup.Position, target - next);
            }

            byte[] data = new byte[imports.Offset + imports.Size];
            imports.Serialize(dataAddress).CopyTo(data, imports.Offset);

            if (relocations != null)
            {
                WriteThreadDirectory(data, threadDirectory, dataAddress, threadDataSize);
                WriteThreadRelocations(context.ReadOnlyData, relocations.Offset, dataAddress + threadDirectory);
            }

            X8664PortableExecutable executable = new X8664PortableExecutable
            {
                Text = context.Assembler.Code.ToArray(),
                ReadOnlyData = context.ReadOnlyData.ToArray(),
                Data = data,
                EntryPoint = AddressOf(context.FunctionSymbol(runtime.Start)),
                ImportDirectory = dataAddress + imports.DescriptorsOffset,
                ImportDirectorySize = imports.DescriptorsSize,
                AddressTable = dataAddress + imports.AddressTableOffset,
                AddressTableSize = imports.AddressTableSize,
                ExceptionDirectory = readOnlyAddress + exceptionDirectory.Offset,
                ExceptionDirectorySize = exceptionTable.Count,
                ThreadDirectory = relocations == null ? 0 : dataAddress + threadDirectory,
                ThreadDirectorySize = relocations == null ? 0 : ThreadDirectorySize,
                RelocationDirectory = relocations == null ? 0 : readOnlyAddress + relocations.Offset,
                RelocationDirectorySize = relocations == null ? 0 : RelocationBlockSize,
                Subsystem = this.Configuration.Subsystem,
                StackReserve = this.Configuration.StackReserve,
            };

            return executable.Serialize();
        }

        const int ThreadDirectoryAlignment = 64;
        const int ThreadDirectorySize = 40;
        const int ThreadIndexOffset = 40;
        const int ThreadCallbacksOffset = 48;
        const int ThreadTemplateOffset = 64;
        const int ThreadAddressCount = 4;
        const int RelocationBlockSize = 8 + ThreadAddressCount * 2;
        const int RelocationDir64 = 10;

        static void WriteThreadDirectory(byte[] data, int directory, int dataAddress, int threadDataSize)
        {
            long Address(int offset) => X8664PortableExecutable.ImageBase + dataAddress + directory + offset;

            long[] addresses = [Address(ThreadTemplateOffset), Address(ThreadTemplateOffset + threadDataSize), Address(ThreadIndexOffset), Address(ThreadCallbacksOffset)];

            for (int i = 0; i < addresses.Length; i++)
            {
                BitConverter.GetBytes(addresses[i]).CopyTo(data, directory + i * 8);
            }
        }

        static void WriteThreadRelocations(List<byte> readOnlyData, int offset, int directoryAddress)
        {
            List<byte> block = [.. BitConverter.GetBytes(directoryAddress & ~0xFFF), .. BitConverter.GetBytes(RelocationBlockSize)];

            for (int i = 0; i < ThreadAddressCount; i++)
            {
                block.AddRange(BitConverter.GetBytes((ushort)((RelocationDir64 << 12) | ((directoryAddress + i * 8) & 0xFFF))));
            }

            for (int i = 0; i < block.Count; i++)
            {
                readOnlyData[offset + i] = block[i];
            }
        }
    }
}
