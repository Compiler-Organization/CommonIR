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

            BuildArrayApp(module);

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

        static void BuildStructApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.Int32, isMutable: false)]);

            IRStructProperty myIntProperty = new IRStructProperty(
                type: new IRType(IRDataTypes.Int32), 
                name: "myInt", 
                defaultValue: builder.BuildConstantInteger(IRDataTypes.Int32, 42)
            );

            IRStructSchema myStruct = module.CreateStructSchema("MyStruct", [myIntProperty]);

            IRValueInstruction _struct = builder.BuildInstantiateStruct(myStruct);

            IRValueInstruction loadedValue = builder.BuildLoad(_struct, myIntProperty.ValueType, myIntProperty);

            builder.BuildCall(consoleLogImport, [loadedValue]);

            builder.BuildStore(_struct, myIntProperty, builder.BuildConstantInteger(IRDataTypes.Int32, 41));
            builder.BuildCall(consoleLogImport, [builder.BuildLoad(_struct, myIntProperty.ValueType, myIntProperty)]);

            builder.BuildReturn();
        }

        static void BuildArrayApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.String, isMutable: false)]);
            IRFunction writeToConsole = module.CreateFunction("WriteToConsole", [IRType.Factory.Void], [new IRLocal("msg", IRType.Factory.String, isMutable: true)], isExport: true);
            IRFunction writeToConsole2 = module.CreateFunction("WriteToConsole2", [IRType.Factory.Void], [new IRLocal("msg", IRType.Factory.String, isMutable: true)], isExport: true);

            IRArraySchema arraySchema = module.CreateArraySchema(IRType.Factory.String);

            IRValueInstruction instantiatedArray = builder.BuildInstantiateArray(arraySchema, 4, [
                builder.BuildConstantString("Hello, World!")
            ]);

            IRValueInstruction loadedValue = builder.BuildLoadArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0));

            builder.BuildCall(writeToConsole, [loadedValue]);
            builder.BuildReturn();


            builder.PositionAtStart(writeToConsole, writeToConsole.Entryblock);
            builder.BuildCall(writeToConsole2, [writeToConsole.Parameters.First()]);
            builder.BuildReturn();


            builder.PositionAtStart(writeToConsole2, writeToConsole2.Entryblock);
            builder.BuildCall(consoleLogImport, [writeToConsole2.Parameters[0]]);
            builder.BuildReturn();
        }
    }
}