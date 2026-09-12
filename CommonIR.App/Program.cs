using CommonIR.Generators;
using CommonIR.Generators.Binary.CIL;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using CommonIR.Passes.Optimization;
// This is used to test functionality as its being developed.

using System.Text;

namespace CommonIR.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
            {
                Target = CommonIRTargets.WebAssembly,
                TargetConfiguration = new(),
                OptimizingMode = OptimizingMode.None,
            };
            CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);

            IRModule module = new IRModule("test");

            BuildConditionalApp(module);

            foreach (SourceFile sourceFile in codeGen.GenerateSourceFiles(module))
            {
                string filename = $"{sourceFile.Name}{sourceFile.Extension}";
                if(sourceFile.Extension == ".wasm")
                {
                    Console.WriteLine($"{filename} ({sourceFile.Data.Length} bytes): 0x{string.Join(", 0x", sourceFile.Data.Select(t => t.ToString("X2")))}");
                }
                else
                {
                    Console.WriteLine($"{filename} ({sourceFile.Data.Length} bytes): {Encoding.UTF8.GetString(sourceFile.Data)}");
                }
                Console.WriteLine();
                sourceFile.WriteToDisk();
            }

            Console.WriteLine(module.Dump(0));
        }

        static (IRFunction, IRBuilder) SetUpInterface(IRModule module)
        {
            IRFunction mainFunction = module.CreateFunction("Main", [], [], isExport: true);
            IRBuilder builder = new IRBuilder(module, mainFunction, mainFunction.Entryblock);
            builder.PositionAtStart(mainFunction, mainFunction.Entryblock);

            return (mainFunction, builder);
        }
        static void BuildConditionalApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.String, isMutable: false)]);

            IRLocal number = function.CreateLocal("number", IRType.Factory.Int32, isMutable: true);
            IRValueInstruction condition = builder.BuildCompare(IRComparisonOperator.LessThan, number, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 10));

            IRBlock thenBlock = function.CreateBlock("thenBlock");
            IRBlock elseBlock = function.CreateBlock("elseBlock");
            builder.SetCheckpoint();

            builder.PositionAtStart(function, thenBlock);
            builder.BuildCall(consoleLogImport, [builder.BuildConstantString("Condition evaluated to true")]);

            builder.PositionAtStart(function, elseBlock);
            builder.BuildCall(consoleLogImport, [builder.BuildConstantString("Condition evaluated to false")]);

            builder.RestoreCheckpoint();
            builder.BuildConditionalBranch(condition, thenBlock, elseBlock);

            builder.BuildReturn();
        }

        static void BuildStructApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.String, isMutable: false)]);

            IRStructProperty messageProperty = new IRStructProperty("message", IRType.Factory.String);
            IRStructSchema structSchema = module.CreateStructSchema("MyStruct", [messageProperty]);

            IRValueInstruction instantiatedStruct = builder.BuildInstantiateStruct(structSchema, [
                builder.BuildConstantString("Hello, world!")
            ]);

            IRValueInstruction loadedValue = builder.BuildLoad(instantiatedStruct, messageProperty.ValueType, messageProperty);

            builder.BuildCall(consoleLogImport, [loadedValue]);

            builder.BuildStore(instantiatedStruct, messageProperty, builder.BuildConstantString("Hello, traditional world!"));
            builder.BuildCall(consoleLogImport, [builder.BuildLoad(instantiatedStruct, messageProperty.ValueType, messageProperty)]);

            builder.BuildReturn();
        }

        static void BuildArrayApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("msg", IRDataTypes.String, isMutable: false)]);

            IRArraySchema arraySchema = module.CreateArraySchema(IRType.Factory.String);

            IRValueInstruction instantiatedArray = builder.BuildInstantiateArray(arraySchema, 4, [
                builder.BuildConstantString("Hello, World!")
            ]);

            IRValueInstruction loadedValue = builder.BuildLoadArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0));
            builder.BuildStoreArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0), loadedValue);

            builder.BuildCall(consoleLogImport, [builder.BuildLoadArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0))]);
            builder.BuildReturn();
        }
    }
}