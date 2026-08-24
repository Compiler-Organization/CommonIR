using CommonIR.Generators;
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
            IRModule module = new IRModule("test");

            BuildDataApp(module);

            CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
            {
                Target = CommonIRTargets.WebAssembly_1_0_MVP,
                OptimizingMode = OptimizingMode.None,
            };
            CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);

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
            IRFunction mainFunction = module.CreateFunction("main", [], [], isExport: true);
            IRBuilder builder = new IRBuilder(module, mainFunction, mainFunction.Entryblock);
            builder.PositionAtStart(mainFunction, mainFunction.Entryblock);

            return (mainFunction, builder);
        }

        static void BuildDataApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.String, isMutable: false)]);


            IRStruct _struct = module.CreateStruct("testStruct");
            IRProperty property = _struct.AddProperty(new IRType(IRDataTypes.String), "num");

            // creating and storing to a struct
            IRLocal newStruct = function.CreateLocal("newStruct", new IRType(IRDataTypes.UserObject), isMutable: true);
            IRValueInstruction malloc = builder.BuildMalloc(builder.BuildConstantInteger(IRDataTypes.Int32, _struct.Width));

            builder.BuildStore(newStruct, malloc);
            builder.BuildStore(newStruct, property, builder.BuildString("Hello, world!"));

            // loading from a struct
            IRValueInstruction loadedProperty = builder.BuildLoad(newStruct, property);
            builder.BuildCall(consoleLogImport, [loadedProperty]);
            builder.BuildReturn();
        }

        static void BuildAssignmentApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.Int32, isMutable: false)]);

            IRValueInstruction testGlobalValue = builder.BuildConstantInteger(IRDataTypes.Int32, 42);
            IRGlobal testGlobal = module.CreateGlobal("testGlobal", new IRType(IRDataTypes.Int32), testGlobalValue, isMutable: true);
            builder.BuildCall(consoleLogImport, [testGlobal]);

            IRValueInstruction testGlobalNewValue = builder.BuildConstantInteger(IRDataTypes.Int32, 83);
            builder.BuildStore(testGlobal, testGlobalNewValue);
            builder.BuildCall(consoleLogImport, [testGlobal]);
        }

        static void BuildBasicCFApp(IRModule module)
        {
            IRGlobal testGlobal = module.CreateGlobal("testVal", new IRType(IRDataTypes.Int32), new IRConstantInteger(IRDataTypes.Int32, 43), isMutable: true);
            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.String, isMutable: false)]);


            IRFunction testFunction = module.CreateFunction("test_conditional", [], [new IRLocal("para1", IRDataTypes.Int32, false)], isExport: true);
            IRBuilder builder = new IRBuilder(module, testFunction, testFunction.Entryblock);
            builder.PositionAtStart(testFunction, testFunction.Entryblock);

            IRValueInstruction comparison = builder.BuildCompare(
                IRComparisonOperator.GreaterThan,
                testFunction.Parameters[0],
                builder.BuildConstantInteger(IRDataTypes.Int32, 47)
            );

            IRBlock thenBlock = testFunction.CreateBlock("if.then");
            IRBlock elseBlock = testFunction.CreateBlock("if.else");
            IRVoidInstruction condBr = builder.BuildConditionalBranch(comparison, thenBlock, elseBlock);

            builder.PositionAtStart(testFunction, thenBlock);
            IRValueInstruction successMarker = builder.BuildConstantInteger(IRDataTypes.Int32, 100);
            builder.BuildCall(consoleLogImport, [builder.BuildString("Success!")]);

            builder.PositionAtStart(testFunction, elseBlock);
            IRValueInstruction failMarker = builder.BuildConstantInteger(IRDataTypes.Int32, 273);
            builder.BuildCall(consoleLogImport, [builder.BuildString("Failed!")]);
        }
    }
}