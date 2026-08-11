# Usage
This document iterates how one would integrate CommonIR into their own compiler project.

## Creating a "Hello, world!" application in WebAssembly.
```cs
using CommonIR.Generators;
using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using CommonIR.Passes.Optimization;

IRModule module = new IRModule("my-first-app");

IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", new IRType(IRDataTypes.Void), [new IRLocal("msg", IRDataTypes.String, isMutable: false)]);
IRFunction testFunction = module.CreateFunction("test", [], [], isExport: true);
IRBuilder builder = new IRBuilder(module, testFunction, testFunction.Entryblock);
builder.PositionAtStart(testFunction, testFunction.Entryblock);

builder.BuildCall(consoleLogImport, [builder.BuildString("Hello, world!")]);

CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
{
    Target = CommonIRTargets.WebAssembly_1_0_MVP,
    OptimizingMode = OptimizingMode.None,
};
CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);

foreach (SourceFile sourceFile in codeGen.GenerateSourceFiles(module))
{
    sourceFile.WriteToDisk();
}
```

`test` is now readily available to be imported from the generated bindings.