using CommonIR.Generators;
using CommonIR.Generators.WASM;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Translation;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System.Text;

namespace CommonIR.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IRModule module = new IRModule("test");
            IRGlobal testGlobal = module.CreateGlobal("testVal", new IRType(IRDataTypes.Int32), new IRConstantInteger(IRDataTypes.Int32, 43), false);
            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.Int32, false)]);


            IRFunction testFunction = module.CreateFunction("test_conditional", [new IRType(IRDataTypes.Void)], [new IRLocal("para1", IRDataTypes.Int32, false)]);
            IRBuilder builder = new IRBuilder(module, testFunction, testFunction.Entryblock);
            builder.PositionAtStart(testFunction, testFunction.Entryblock);

            IRValueInstruction constantValue1 = builder.BuildLoad(testFunction.Parameters[0]);
            IRValueInstruction constantValue2 = builder.BuildConstantInteger(IRDataTypes.Int32, 47);

            IRValueInstruction comparison = builder.BuildCompare(
                IRComparisonOperator.GreaterThan,
                constantValue1,
                constantValue2
            );
            IRBlock thenBlock = testFunction.CreateBlock("if.then");
            IRBlock elseBlock = testFunction.CreateBlock("if.else");
            IRVoidInstruction condBr = builder.BuildConditionalBranch(comparison, thenBlock, elseBlock);

            builder.PositionAtStart(testFunction, thenBlock);
            IRValueInstruction successMarker = builder.BuildConstantInteger(IRDataTypes.Int32, 100);
            builder.BuildCall(consoleLogImport, [successMarker]);

            builder.PositionAtStart(testFunction, elseBlock);
            IRValueInstruction failMarker = builder.BuildConstantInteger(IRDataTypes.Int32, 273);
            builder.BuildCall(consoleLogImport, [failMarker]);


            CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
            {
                Target = CommonIRTargets.WebAssembly_1_0_MVP
            };
            CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);

            Console.WriteLine(module.Dump());

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
        }
    }
}