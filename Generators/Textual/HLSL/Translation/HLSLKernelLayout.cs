using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
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

        public const int MaximumRootSize = 64;

        public IRFunction Kernel { get; }

        public List<IRLocal> Inputs { get; }

        public List<IRLocal> Scalars { get; }

        public IRType? OutputElementType { get; }

        public HashSet<IRLocal> Written { get; }

        public bool HasOutput => this.Kernel.HasKernelOutput;

        public int ConstantCount => 2 + this.Inputs.Count + this.Scalars.Count;

        public int OutputRegister => 1;

        public int InputRegister(int input) => (this.HasOutput ? 2 : 1) + input;

        public int BufferCount => 1 + (this.HasOutput ? 1 : 0) + this.Inputs.Count;

        public int RootSize => this.ConstantCount + 2 * this.BufferCount;

        public string RootSignature => $"RootConstants(num32BitConstants={this.ConstantCount}, b0){string.Concat(Enumerable.Range(0, this.BufferCount).Select(i => $", UAV(u{i})"))}";

        public HLSLKernelLayout(IRFunction kernel)
        {
            if (!kernel.IsKernel)
            {
                throw ErrorHandler.Create($"'{kernel.Name}' is not a kernel.");
            }

            this.Kernel = kernel;

            if (kernel.HasKernelOutput)
            {
                this.OutputElementType = ElementTypeOf(kernel.KernelOutput.ValueType, kernel.Name);
                StrideOf(this.OutputElementType, kernel.Name);
            }

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

            if (this.RootSize > MaximumRootSize)
            {
                throw ErrorHandler.Create($"Kernel '{kernel.Name}' has too many parameters for the GPU: it needs {this.RootSize} of the {MaximumRootSize} values a GPU function gets. Each array takes 3, each other parameter 1, and the function itself 4 (6 with an output buffer).");
            }

            this.Written = new HashSet<IRLocal>();
            FindWrites(kernel.Entryblock);
        }

        void FindWrites(IRBlock block)
        {
            foreach (IRInstruction instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case IRStoreElement { Target: IRLocal target } when this.Inputs.Contains(target):
                        this.Written.Add(target);
                        break;

                    case IRConditionalBranch branch:
                        FindWrites(branch.ThenBlock);

                        if (branch.HasElseBlock)
                        {
                            FindWrites(branch.ElseBlock);
                        }
                        break;

                    case IRLoop loop:
                        FindWrites(loop.Block);

                        if (loop.Increment != null)
                        {
                            FindWrites(loop.Increment);
                        }
                        break;

                    case IRBlock nested:
                        FindWrites(nested);
                        break;
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
