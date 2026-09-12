# Usage
This document goes over how one would integrate CommonIR into their own compiler project.
This document serves as an example-riddled guide to using the IR.

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
IRValueInstruction helloWorldString = builder.BuildConstantString("Hello, World!");
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

## Creating and instantiating a struct
Before we can instantiate a struct, we first need to create a schema for it containing the properties we want the struct to contain.

First, create your properties, declaring a name and the type of the property.
```csharp
IRStructProperty messageProperty = new IRStructProperty("message", IRType.Factory.String);
```

Then, create a named struct schema using the previous property. The order of properties reflects its order in memory.
```csharp 
IRStruct structSchema = module.CreateStructSchema("MyStruct", [messageProperty]);
```

After this, you can use the struct in your IR code. For example, to create an instance of the struct and initialize it, you can do the following:
```csharp
IRValueInstruction instantiatedStruct = builder.BuildInstantiateStruct(structSchema, [
    builder.BuildConstantString("Hello, world!") // Initialize messageProperty with a constant string.
]);
```
This will create a new instance of the struct and initialize its properties with the specified values. Keep in mind that property initialization is mapped by index to the property being initialized (value 0 == property 0).
If no default value is specified, the property will be initialized to zero.

``BuildInstantiateStruct`` returns a thin pointer to the struct, which can be used to later access the properties of the struct.

## Loading and storing struct properties
Loading and storing values to properties in structs is simple and straight-forward.
```csharp
IRValueInstruction loadedValue = builder.BuildLoad(instantiatedStruct, messageProperty.ValueType, messageProperty);
```

This value can then be read and, for example, printed to the console like so.
```csharp
IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("msg", IRDataTypes.String, isMutable: false)]); // WASM console.log(str) import
builder.BuildCall(consoleLogImport, [loadedValue]); // "Hello, world!"
```

If you want to later modify a value of a property in a instantiated struct, you can do the following.
```csharp
builder.BuildStore(instantiatedStruct, messageProperty, builder.BuildConstantString("Goodbye, world!"));
```

Loading the property again now will get the "Goodbye, world!" string instead.

___

## Creating and instantiating an array.
Before instantiating an array, you first need to create a schema for it describing the type of elements it will hold.

Create a named array schema by specifying the element type:
```csharp
IRArraySchema arraySchema = module.CreateArraySchema(IRType.Factory.String);
```

Once you have a schema, you can use it in your IR code. To create an instance of the array, specify the schema, the array's length, and any initial values:
```csharp
IRValueInstruction instantiatedArray = builder.BuildInstantiateArray(arraySchema, 4, [
    builder.BuildConstantString("Hello, World!")
]);
```

This creates a new array of the given length (4 elements, in this case) and initializes its elements with the specified values. As with structs, initialization values are mapped by index to the element being initialized (value 0 == element 0). Any elements without a specified initial value are initialized to zero.

The static size ``4`` can be replaced with an IRValueInstruction to instantiate an array with a dynamic size.

BuildInstantiateArray returns a fat pointer to the array, which can be used to later access its elements.

## Loading and storing array elements

Loading and storing values to elements in an array is simple and straightforward. Since array elements are accessed by index rather than by name, you'll need to supply an index value (as an `IRValueInstruction`) alongside the element type.

To load an element from the array:
```csharp
IRValueInstruction loadedValue = builder.BuildLoadArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0));
```

Here, `builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0)` creates the index 0, pointing at the first element of the array.

This value can then be used elsewhere, for example passed to an imported function:
```csharp
IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("msg", IRDataTypes.String, isMutable: false)]); // WASM console.log(str) import
builder.BuildCall(consoleLogImport, [loadedValue]); // "Hello, World!"
```

If you want to modify the value of an element in an instantiated array, you can do the following:
```csharp
builder.BuildStoreArrayElement(instantiatedArray, IRType.Factory.String, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 0), loadedValue);
```
This stores `loadedValue` back into element `0` of the array. As with structs, loading the element again afterward will return the newly stored value.

## Loops

Below is a sample app creating a loop with an iterator, looping 10 times from 0 to 9.
```csharp
static void BuildLoopApp(IRModule module)
{
    (IRFunction function, IRBuilder builder) = SetUpInterface(module); // Not a part of CommonIR.
    module.EntryPoint = function;

    IRFunctionImport consoleLogImport = module.CreateFunctionImport("console", "log", IRType.Factory.Void, [new IRLocal("x", IRDataTypes.Int32, isMutable: false)]);

    IRLocal iterator = function.CreateLocal("iterator", IRType.Factory.Int32, isMutable: true); // iterator = 0
    IRValueInstruction loopCondition = builder.BuildCompare(IRComparisonOperator.LessThan, iterator, builder.BuildConstantInteger(IRType.Factory.Int32.DataType, 10)); // iterator < 10

    IRBlock loopBlock = function.CreateBlock("loopblock");
    builder.SetCheckpoint(); // Keeping track of where we are before moving to the loop block. 
    builder.PositionAtStart(function, loopBlock);

    // The body of the loop
    builder.BuildCall(consoleLogImport, [iterator]);
    builder.BuildStore(iterator, builder.BuildAdd(iterator, builder.BuildConstantInteger(IRDataTypes.Int32, 1)));

    builder.RestoreCheckpoint(); // Going back to where we left off.
    builder.BuildLoop(loopCondition, loopBlock);

    builder.BuildReturn();
}
```

## Conditional branching

Below is a sample app creating a simple conditional branch, determining if a local with value 0 is less than 10.
```csharp
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

    builder.PositionAtStart(function, thenBlock); // Building the then-block
    builder.BuildCall(consoleLogImport, [builder.BuildConstantString("Condition evaluated to true")]);

    builder.PositionAtStart(function, elseBlock); // Building the else-block
    builder.BuildCall(consoleLogImport, [builder.BuildConstantString("Condition evaluated to false")]);

    builder.RestoreCheckpoint();
    builder.BuildConditionalBranch(condition, thenBlock, elseBlock);

    builder.BuildReturn();

    // "Condition evaluated to true"
}
```