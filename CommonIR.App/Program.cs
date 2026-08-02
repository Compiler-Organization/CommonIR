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
            IRFunction function = module.CreateFunction("addAndLog", new IRType(IRDataTypes.Void), [new IRLocal("a", IRDataTypes.Int32, false), new IRLocal("b", IRDataTypes.Int32, false)]);
            IRBuilder builder = new IRBuilder(module, function, function.Entryblock);

            IRValueInstruction loadParam1 = builder.BuildLoad(function.Parameters[0]);
            IRValueInstruction loadParam2 = builder.BuildLoad(function.Parameters[1]);

            IRValueInstruction addResult = builder.BuildAdd(loadParam1, loadParam2);
            builder.BuildCall(consoleLogImport, [addResult]);
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