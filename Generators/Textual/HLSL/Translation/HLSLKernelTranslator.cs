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

            builder.AppendLine($"RWStructuredBuffer<{HLSLInstructionEmitter.TypeName(layout.OutputElementType)}> {emitter.NameOf(kernel.KernelOutput)} : register(u0);");

            for (int i = 0; i < layout.Inputs.Count; i++)
            {
                IRType elementType = HLSLKernelLayout.ElementTypeOf(layout.Inputs[i].ValueType, kernel.Name);
                builder.AppendLine($"StructuredBuffer<{HLSLInstructionEmitter.TypeName(elementType)}> {emitter.NameOf(layout.Inputs[i])} : register(t{i});");
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
    }
}
