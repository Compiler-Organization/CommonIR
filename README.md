# CommonIR

Intermediate representation to generate code for various different platforms.

This project has recently just started, and as such, is not yet complete.

___

Documentation will be added as the project progresses.

For a live demonstration of the IR in use, see [the current implimentation in Common C](https://github.com/Compiler-Organization/CommonC/blob/master/Targets/CommonIR/CodeGen/CommonIRCodeGen.cs).

For more about how to use CommonIR, see [the usage examples](https://github.com/Compiler-Organization/CommonIR/blob/master/Documentation/Usage.md).

For more info about target development philosophy, see [the general target spec](https://github.com/Compiler-Organization/CommonIR/blob/master/Documentation/General%20target%20spec.md).

# Using CommonIR (with examples)
To understand how to use CommonIR, we must first establish the fundamental facts about the IR and how it lowers into target code.

CommonIR consists of a hirearchical structure of objects, which are used to represent the program in a target agnostic way.

* IRModule
    * IRFunction
        * IRLocal
        * IRBlock
            * IRInstruction
    * IRFunctionImport
    * IRGlobal

Each created instruction will keep its value if used multiple times. To create new values, you must build a new instructions.

To keep things simple, aggregates have their own way of being built. You do not need to manually allocate and construct your own aggregates.
This is also to later maintain compliance with ABIs.