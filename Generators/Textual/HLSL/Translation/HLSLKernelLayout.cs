using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    public class HLSLKernelLayout
    {
        public const int CountConstant = 0;

        public const int RowConstant = 1;

        public const int MaximumGroupsPerDimension = 65535;

        public IRFunction Kernel { get; }

        public List<IRLocal> Inputs { get; }

        public List<IRLocal> Scalars { get; }

        public IRType OutputElementType { get; }

        public int ConstantCount => 2 + this.Inputs.Count + this.Scalars.Count;

        public string RootSignature => $"RootConstants(num32BitConstants={this.ConstantCount}, b0), UAV(u0){string.Concat(this.Inputs.Select((_, i) => $", SRV(t{i})"))}";

        public HLSLKernelLayout(IRFunction kernel)
        {
            if (!kernel.IsKernel)
            {
                throw ErrorHandler.Create($"'{kernel.Name}' is not a kernel.");
            }

            this.Kernel = kernel;
            this.OutputElementType = ElementTypeOf(kernel.KernelOutput.ValueType, kernel.Name);

            StrideOf(this.OutputElementType, kernel.Name);

            this.Inputs = new List<IRLocal>();
            this.Scalars = new List<IRLocal>();

            foreach (IRLocal parameter in kernel.KernelParameters)
            {
                if (parameter.ValueType.DataType == IRDataTypes.Array)
                {
                    StrideOf(ElementTypeOf(parameter.ValueType, kernel.Name), kernel.Name);
                    this.Inputs.Add(parameter);
                }
                else if (parameter.ValueType.DataType is IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Float32 or IRDataTypes.Bool)
                {
                    this.Scalars.Add(parameter);
                }
                else
                {
                    throw ErrorHandler.CreateNotImplimented($"Kernel '{kernel.Name}' has a parameter '{parameter.Name}' of type '{parameter.ValueType.Dump(0)}', which cannot be passed to the GPU.");
                }
            }
        }

        public int LengthConstant(IRLocal input) => 2 + this.Inputs.IndexOf(input);

        public int ScalarConstant(IRLocal scalar) => 2 + this.Inputs.Count + this.Scalars.IndexOf(scalar);

        public static IRType ElementTypeOf(IRType arrayType, string kernelName)
        {
            if (arrayType.DataType != IRDataTypes.Array || arrayType.UserObject is not IRArraySchema schema)
            {
                throw ErrorHandler.Create($"Kernel '{kernelName}' expected an array type, but got '{arrayType.Dump(0)}'.");
            }

            return schema.ElementType;
        }

        public static int StrideOf(IRType elementType, string kernelName)
        {
            return elementType.DataType switch
            {
                IRDataTypes.Int32 or IRDataTypes.UInt32 or IRDataTypes.Float32 => 4,
                _ => throw ErrorHandler.CreateNotImplimented($"Kernel '{kernelName}' uses arrays of '{elementType.Dump(0)}', which cannot be passed to the GPU. Use 32-bit integers or floats.")
            };
        }
    }
}
