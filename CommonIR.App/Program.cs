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
            IRModule module = new IRModule("test");

            BuildDataApp(module);

            CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
            {
                Target = CommonIRTargets.CommonIntermediateLanguage,
                TargetConfiguration = new CommonIRCILConfiguration(),
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
            IRFunction mainFunction = module.CreateFunction("Main", [], [], isExport: true);
            IRBuilder builder = new IRBuilder(module, mainFunction, mainFunction.Entryblock);
            builder.PositionAtStart(mainFunction, mainFunction.Entryblock);

            return (mainFunction, builder);
        }

        static void BuildDataApp(IRModule module)
        {
            (IRFunction function, IRBuilder builder) = SetUpInterface(module);
            module.EntryPoint = function;

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("System.Console", "WriteLine", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.String, isMutable: false)]);

            builder.BuildCall(consoleLogImport, [builder.BuildString("Hello, world!")]);
            builder.BuildReturn();
        }
    }
}