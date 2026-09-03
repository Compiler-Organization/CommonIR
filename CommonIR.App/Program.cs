using CommonIR.Generators;
using CommonIR.Generators.CIL;
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

            BuildDataApp(module);

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

        static void BuildDataApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.Int32, isMutable: false)]);

            IRStructProperty myIntProperty = new IRStructProperty(
                type: new IRType(IRDataTypes.Int32), 
                name: "myInt", 
                defaultValue: builder.BuildConstantInteger(IRDataTypes.Int32, 42)
            );

            IRStruct myStruct = module.CreateStruct("MyStruct", [myIntProperty]);

            IRValueInstruction _struct = builder.BuildInitializeStruct(myStruct);

            // builder.BuildStore(_struct, myIntProperty, builder.BuildConstantInteger(IRDataTypes.Int32, 42));

            IRValueInstruction loadedValue = builder.BuildLoad(_struct, myIntProperty.ValueType, myIntProperty);

            //IRValueInstruction array = builder.BuildCreateArray(IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 4));
            //builder.BuildStoreArrayElement(array, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0), builder.BuildString("Hello, World!"));

            //IRValueInstruction loadedValue = builder.BuildLoadArrayElement(array, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0));

            builder.BuildCall(consoleLogImport, [loadedValue]);

            builder.BuildStore(_struct, myIntProperty, builder.BuildConstantInteger(IRDataTypes.Int32, 41));
            builder.BuildCall(consoleLogImport, [builder.BuildLoad(_struct, myIntProperty.ValueType, myIntProperty)]);

            builder.BuildReturn();
        }
    }
}