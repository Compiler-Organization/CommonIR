using CommonIR.Errors;
using CommonIR.Generators.Textual.HLSL.Emission;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    internal class HLSLStandaloneFunctionTranslator
    {
        CommonIRHLSLConfiguration Configuration { get; set; }

        public HLSLStandaloneFunctionTranslator(CommonIRHLSLConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public string TranslateFunction(IRFunction function)
        {
            StringBuilder builder = new StringBuilder();

            if(!function.HasReturn())
            {
                throw ErrorHandler.CreateNotImplimented($"HLSL target with standalone function translation does not support functions without any returns.");
            }

            if(function.ReturnTypes.Count > 1)
            {
                throw ErrorHandler.CreateNotImplimented($"HLSL target with standalone function translation does not support functions with tupled returns.");
            }

            IRType returnType = function.ReturnTypes.First();

            if(returnType.DataType != IRDataTypes.Array)
            {
                throw ErrorHandler.CreateNotImplimented($"HLSL target with standalone function translation does not support functions without an array return type.");
            }

            builder.AppendLine(HLSLTypeTranslator.ToHLSLDeclaration(returnType, "OutputBuffer", true));

            List<IRStructProperty> cbufferProperties = function.Parameters.Where(p => p.ValueType.IsScalarType).Select(p => new IRStructProperty(p.Name!, p.ValueType)).ToList();
            IRStructSchema cbufferSchema = new IRStructSchema("InputData", cbufferProperties);

            if(cbufferSchema.Properties.Count > 0)
            {
                builder.AppendLine(HLSLTypeTranslator.ToHLSLCBuffer(cbufferSchema));
            }

            builder.AppendLine($"[numthreads({this.Configuration.ThreadGroupSizes.X}, {this.Configuration.ThreadGroupSizes.Y}, {this.Configuration.ThreadGroupSizes.Z})]");
            builder.AppendLine("void main()");
            builder.AppendLine("{");

            HLSLInstructionEmitter instructionEmitter = new HLSLInstructionEmitter(function);
            builder.AppendLine($"\t{string.Join("\n\t", instructionEmitter.EmitInstructions(function.Entryblock.Instructions))}");

            builder.AppendLine("}");

            return builder.ToString();
        }
    }
}
