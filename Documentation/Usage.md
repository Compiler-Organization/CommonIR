# Usage
This document iterates how one would integrate CommonIR into their own compiler project.
This document servers as an example-riddled guide to using the IR.

# Table of Contents
* [Getting Started](#getting-started)
* [Specifying a target](#specifying-a-target)
* [Building an IR Module](#building-an-ir-module)
* [Building an IR Function](#building-an-ir-function)
* [Importing functions](#importing-functions)
* [Creating an IR Builder](#creating-an-ir-builder)
* [Emitting Code](#emitting-code)
* [Saving to a file](#saving-to-a-file)

# Getting Started
Welcome to the CommonIR usage guide! This document will walk you through the steps necessary to integrate CommonIR into your own compiler project.

To lay out the fundamental principles of CommonIR, we will discuss how the IR is structured, why it is designed the way it is, and how to use it effectively.
This project is designed to be a flexible and extensible intermediate representation which can be used in a variety of compiler projects.

CommonIR is an AST-like intermediate representation using two representations for instructions, ``IRVoidInstruction`` and ``IRValueInstruction``.

Any IRValueInstruction must, at some point, be used by an IRVoidInstruction to be generated with the final output.
This is to ensure that all instructions are used and that no instructions are left unused in the final output.

Any IRValueInstruction can be used multiple times, but its result will not change and is immutable.

CommonIR has aggregates like IRStruct and IRArray to maintain ABI compatibility and make both target code generation and development easier.

# Specifying a target
Before anything can be done, the code generator must be initialized with settings.

* [Target](https://github.com/Compiler-Organization/CommonIR/blob/master/Generators/CommonIRTargets.cs) is an enum which specifies the target architecture for the code generator.
* [TargetConfiguration](https://github.com/Compiler-Organization/CommonIR/blob/master/Generators/CommonIRTargetConfiguration.cs) is a class containing target-specific settings for the code generator.
If the target does **not** require any specific settings, we'll use the default constructor ``new()``.
See the [example here](https://github.com/Compiler-Organization/CommonIR/blob/master/Generators/CIL/CommonIRCILConfiguration.cs) to see how to create a configuration for the Common Intermediate Language target.
* [OptimizingMode](https://github.com/Compiler-Organization/CommonIR/blob/master/Passes/Optimization/IROptimizer.cs#L54-L85) specifies the optimization level for the code generator. The default is ``OptimizingMode.None``, meaning no optimizations will be performed.

```csharp
CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
{
    Target = CommonIRTargets.WebAssembly,
    TargetConfiguration = new(),
    OptimizingMode = OptimizingMode.None,
};
CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);
```

# Building an IR Module
The [IRModule](https://github.com/Compiler-Organization/CommonIR/blob/master/IR/Grammar/Objects/IRModule.cs) is the top-level container for all IR code. It contains all functions, global variables, and other top-level constructs.
All generation happens within the context of an IRModule.
```csharp
IRModule module = new IRModule("MyModule");
```

After all generation is complete, the code generator can be used to generate [source files](https://github.com/Compiler-Organization/CommonIR/blob/master/SourceFile.cs) based on the IR module.
```csharp
List<SourceFile> sourceFiles = codeGen.GenerateSourceFiles(module);
```

# Building an IR Function
The [IRFunction](https://github.com/Compiler-Organization/CommonIR/blob/master/IR/Grammar/Objects/IRFunction.cs) is the container for all IR instructions. It uses IRBlocks to group instructions together and control the flow of execution.
Any interaction with an IRFunction should happen through either its methods or through an IRBuilder.

Instructions are contained within an IRFunction's ``Entryblock``.

```csharp
IRFunction mainFunction = module.CreateFunction(
    name: "Main", 
    returnTypes: [], 
    parameterTypes: [], 
    isExport: true
);
```

# Importing functions
Importing functions uses [IRFunctionImport](https://github.com/Compiler-Organization/CommonIR/blob/master/IR/Grammar/Objects/IRFunctionImport.cs) and is straight-forward and target-independent. Below is an example of importing a function from JavaScript into a WebAssembly module.
Here, bindings are generated automatically. IRFunctionImport is derived from IRFunction, and as such, can be used in the same way as any other IRFunction.

IRFunctionImports may not contain instructions.

```csharp
IRFunctionImport consoleLogImport = module.CreateFunctionImport(
    moduleName: "console", 
    functionName: "log", 
    returnType: IRType.Factory.Void, 
    parameterTypes: [
        new IRLocal(
            name: "x", 
            type: IRDataTypes.Int32, 
            isMutable: false)
    ]
);
```

# Creating an IR Builder
The [IRBuilder](https://github.com/Compiler-Organization/CommonIR/blob/master/IR/IRBuilder.cs) is the foundation of constructing IR instructions. It provides a fluent interface for creating instructions and managing control flow.

Keep in mind that the IRBuilder is for **instructions only** (such as struct initialization). Creating objects or aggregates happens through either the IRModule or IRFunction.

```csharp
IRBuilder builder = new IRBuilder(
    module: module, 
    function: mainFunction, 
    block: mainFunction.Entryblock
);
```

And to position the builder at the start of the function's entry block, we do the following:
```csharp
builder.PositionAtStart(
    function: mainFunction, 
    block: mainFunction.Entryblock
);
```

# Emitting Code
Like discussed earlier, emitting instructions is done through the IRBuilder.
IR generation can be complex, so we'll start off gentle with an example of emitting a simple Hello World program, targeting WebAssembly.
```csharp
// Setting up the code generator.
CommonIRCodeGeneratorSettings codeGenSettings = new CommonIRCodeGeneratorSettings
{
    Target = CommonIRTargets.WebAssembly,
    TargetConfiguration = new(),
    OptimizingMode = OptimizingMode.None,
};
CommonIRCodeGenerator codeGen = new CommonIRCodeGenerator(codeGenSettings);

// Creating the IRModule and IRFunction.
IRModule module = new IRModule("MyWebAssemblyModule");
IRFunction mainFunction = module.CreateFunction("Main", [], [], isExport: true);

// Setting 'Main' as the entry point.
module.EntryPoint = mainFunction;

// Creating the IRBuilder and positioning it at the start of 'Main'.
IRBuilder builder = new IRBuilder(module, mainFunction, mainFunction.Entryblock);
builder.PositionAtStart(mainFunction, mainFunction.Entryblock);

// Importing 'console.log' with one parameter from JavaScript.
IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("message", IRDataTypes.String, isMutable: false)]);

// Building a string and calling 'console.log' with it.
IRValueInstruction helloWorldString = builder.BuildString("Hello, World!");
builder.BuildCall(consoleLogImport, [helloWorldString]);
builder.BuildReturn();

// Generating the source files from the IRModule and writing them to the disk.
foreach (SourceFile sourceFile in codeGen.GenerateSourceFiles(module))
{
    sourceFile.WriteToDisk();
}
```

After running the above code, you should have two files on your disk: ``MyWebAssemblyModule_module.wasm`` and ``MyWebAssemblyModule_bindings.js``.

To see if this module works, create a new file named ``index.html`` and add the following code to it:
```html
<script type="module">
    import init, {Main} from './MyWebAssemblyModule_bindings.js';
    init();
</script>
```

If you now open the developer console, you should see "Hello, World!" printed to the console.

## Creating a struct
Creating a struct is done by creating an IRStructProperty for each property, and then calling `BuildInitializeStruct` with the name of the struct and a list of the properties.

First, create your properties.
```csharp
IRStructProperty myIntProperty = new IRStructProperty(
    type: new IRType(IRDataTypes.Int32), 
    name: "myInt", 
    defaultValue: builder.BuildConstantInteger(IRDataTypes.Int32, 42)
);
```

Then, create the struct.
```csharp 
IRStruct myStruct = module.CreateStruct("MyStruct", [myIntProperty]);
```

After this, you can use the struct in your IR code. For example, to create an instance of the struct and initialize it, you can do the following:
```csharp
IRValueInstruction initializedStruct = builder.BuildInitializeStruct(myStruct);
```
This will create a new instance of the struct and initialize it with the default values specified in the properties.
If no default value is specified, the property will be initialized to zero.

``BuildInitializeStruct`` returns a thin pointer to the struct, which can be used to later access the properties of the struct.

Loading and storing values to properties in structs is simple and straight-forward.
```csharp
IRValueInstruction loadedValue = builder.BuildLoad(initializedStruct, myIntProperty.ValueType, myIntProperty); // 42
```

```csharp
builder.BuildStore(initializedStruct, myIntProperty, builder.BuildConstantInteger(IRDataTypes.Int32, 41));
```