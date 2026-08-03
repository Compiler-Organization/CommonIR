using CommonIR.Generators.WASM;
using CommonIR.Generators.WASM.Model;
using CommonIR.Generators.WASM.Translation;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.App
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IRModule module = new IRModule();

            IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("x", IRDataTypes.Int32, false)]);
            IRFunction addFunction = module.CreateFunction("add", new IRType(IRDataTypes.Int32), [new IRLocal("a", IRDataTypes.Int32, false), new IRLocal("b", IRDataTypes.Int32, false)]);

            IRBuilder builder = new IRBuilder(module, addFunction, addFunction.Entryblock);

            IRValueInstruction loadParam1 = builder.BuildLoad(addFunction.Parameters[0]);
            IRValueInstruction loadParam2 = builder.BuildLoad(addFunction.Parameters[1]);

            IRValueInstruction addResult = builder.BuildAdd(loadParam1, loadParam2);
            builder.BuildReturn(addResult);

            IRFunction printFunction = module.CreateFunction("print", new IRType(IRDataTypes.Void), [new IRLocal("a", IRDataTypes.Int32, false)]);
            builder.PositionAtStart(printFunction, printFunction.Entryblock);

            IRValueInstruction loadParame1 = builder.BuildLoad(printFunction.Parameters[0]);
            builder.BuildCall(consoleLogImport, [loadParame1]);
            builder.BuildReturn();

            WasmGenerator wasmGenerator = new WasmGenerator(module);

            foreach (SourceFile sourceFile in wasmGenerator.GenerateSourceFiles())
            {
                string filename = $"{sourceFile.Name}{sourceFile.Extension}";
                Console.WriteLine($"{filename} ({sourceFile.Data.Length} bytes): {string.Join(" ", sourceFile.Data.Select(t => t.ToString("X2")))}");
                Console.WriteLine();
                sourceFile.WriteToDisk();
            }
        }
    }
}