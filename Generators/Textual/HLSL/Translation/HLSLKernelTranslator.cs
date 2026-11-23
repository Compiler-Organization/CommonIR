using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Emission;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    public static class HLSLKernelTranslator
    {
        public static string Translate(IRFunction kernel, CommonIRHLSLConfiguration configuration)
        {
            if (configuration.ShaderStage != HLSLShaderStage.Compute)
            {
                throw ErrorHandler.CreateNotImplimented($"Kernels can only be emitted as compute shaders, not as '{configuration.ShaderStage}' shaders.");
            }

            HLSLKernelLayout layout = new HLSLKernelLayout(kernel);
            HLSLInstructionEmitter emitter = new HLSLInstructionEmitter(kernel, layout);
            StringBuilder builder = new StringBuilder();

            builder.AppendLine($"#define ROOT_SIGNATURE \"{layout.RootSignature}\"");
            builder.AppendLine();

            builder.AppendLine("cbuffer Constants : register(b0)");
            builder.AppendLine("{");
            builder.AppendLine("    uint __count;");
            builder.AppendLine("    uint __row;");

            for (int i = 0; i < layout.Inputs.Count; i++)
            {
                builder.AppendLine($"    uint __length{i};");
            }

            for (int i = 0; i < layout.Scalars.Count; i++)
            {
                string type = layout.Scalars[i].ValueType.DataType == IRDataTypes.Bool ? "uint" : HLSLInstructionEmitter.TypeName(layout.Scalars[i].ValueType);
                builder.AppendLine($"    {type} __scalar{i};");
            }

            builder.AppendLine("};");
            builder.AppendLine();

            builder.AppendLine($"RWStructuredBuffer<uint> {HLSLInstructionEmitter.FaultBuffer} : register(u0);");

            if (layout.HasOutput)
            {
                builder.AppendLine($"RWStructuredBuffer<{HLSLInstructionEmitter.TypeName(layout.OutputElementType!)}> {emitter.NameOf(kernel.KernelOutput)} : register(u{layout.OutputRegister});");
            }

            for (int i = 0; i < layout.Inputs.Count; i++)
            {
                IRType elementType = HLSLKernelLayout.ElementTypeOf(layout.Inputs[i].ValueType, kernel.Name);
                builder.AppendLine($"RWStructuredBuffer<{HLSLInstructionEmitter.TypeName(elementType)}> {emitter.NameOf(layout.Inputs[i])} : register(u{layout.InputRegister(i)});");
            }

            builder.AppendLine();
            builder.AppendLine($"void {HLSLInstructionEmitter.FaultFunction}()");
            builder.AppendLine("{");
            builder.AppendLine($"    {HLSLInstructionEmitter.FaultBuffer}[0] = 1u;");
            builder.AppendLine("}");

            if (layout.HasOutput)
            {
                string output = emitter.NameOf(kernel.KernelOutput);
                string outputType = HLSLInstructionEmitter.TypeName(layout.OutputElementType!);
                AppendAccessors(builder, HLSLInstructionEmitter.LoadOutputFunction, HLSLInstructionEmitter.StoreOutputFunction, output, outputType, "__count");
            }

            for (int i = 0; i < layout.Inputs.Count; i++)
            {
                string elementType = HLSLInstructionEmitter.TypeName(HLSLKernelLayout.ElementTypeOf(layout.Inputs[i].ValueType, kernel.Name));
                AppendAccessors(builder, HLSLInstructionEmitter.LoadInputFunction(i), HLSLInstructionEmitter.StoreInputFunction(i), emitter.NameOf(layout.Inputs[i]), elementType, $"__length{i}");
            }

            builder.AppendLine();
            builder.AppendLine("[RootSignature(ROOT_SIGNATURE)]");
            builder.AppendLine($"[numthreads({configuration.ThreadGroupSizes.X}, 1, 1)]");
            builder.AppendLine($"void {configuration.EntryPointName}(uint3 __id : SV_DispatchThreadID)");
            builder.AppendLine("{");
            builder.AppendLine("    uint __flat = __id.y * __row + __id.x;");
            builder.AppendLine();
            builder.AppendLine("    if (__flat >= __count)");
            builder.AppendLine("    {");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine($"    int {emitter.NameOf(kernel.KernelIndex)} = (int)__flat;");

            for (int i = 0; i < layout.Scalars.Count; i++)
            {
                IRLocal scalar = layout.Scalars[i];
                string value = scalar.ValueType.DataType == IRDataTypes.Bool ? $"__scalar{i} != 0" : $"__scalar{i}";
                builder.AppendLine($"    {HLSLInstructionEmitter.TypeName(scalar.ValueType)} {emitter.NameOf(scalar)} = {value};");
            }

            foreach (IRLocal local in kernel.Locals)
            {
                builder.AppendLine($"    {HLSLInstructionEmitter.TypeName(local.ValueType)} {emitter.NameOf(local)} = {HLSLInstructionEmitter.DefaultValue(local.ValueType)};");
            }

            builder.AppendLine();
            emitter.EmitBlock(builder, kernel.Entryblock, 1);
            builder.AppendLine("}");

            return builder.ToString();
        }

        static void AppendAccessors(StringBuilder builder, string load, string store, string buffer, string type, string length)
        {
            builder.AppendLine();
            builder.AppendLine($"{type} {load}(uint i)");
            builder.AppendLine("{");
            builder.AppendLine($"    if (i >= {length})");
            builder.AppendLine("    {");
            builder.AppendLine($"        {HLSLInstructionEmitter.FaultFunction}();");
            builder.AppendLine($"        return ({type})0;");
            builder.AppendLine("    }");
            builder.AppendLine($"    return {buffer}[i];");
            builder.AppendLine("}");
            builder.AppendLine();
            builder.AppendLine($"void {store}(uint i, {type} value)");
            builder.AppendLine("{");
            builder.AppendLine($"    if (i >= {length})");
            builder.AppendLine("    {");
            builder.AppendLine($"        {HLSLInstructionEmitter.FaultFunction}();");
            builder.AppendLine("        return;");
            builder.AppendLine("    }");
            builder.AppendLine($"    {buffer}[i] = value;");
            builder.AppendLine("}");
        }
    }
}
