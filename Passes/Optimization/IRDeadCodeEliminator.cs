using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.Passes.Optimization
{
    public class IRDeadCodeEliminator : IRPass
    {
        IRModule Module { get; set; }

        public IRDeadCodeEliminator(IRModule module)
        {
            this.Module = module;
        }

        public void Pass()
        {
            RemoveUnusedFunctions();
            RemoveUnusedLocals();
            RemoveUnusedGlobals();
        }

        /// <summary>
        /// Removes unused functions from the module and refragments its function repository
        /// </summary>
        void RemoveUnusedFunctions()
        {
            foreach (IRFunction function in this.Module.Functions.Where(f => !f.IsExport && f.References.Count == 0))
            {
                foreach (IRBlock block in function.Blocks)
                {
                    foreach (IRInstruction instruction in block.Instructions)
                    {
                        RecursiveRemoveReferences(instruction);
                    }
                }
            }

            Module.Functions.RemoveAll(f => !f.IsExport && f.References.Count == 0);
        }

        /// <summary> 
        /// Recursively removes references through all operands of an instruction.
        /// </summary>
        /// <param name="instruction"></param>
        void RecursiveRemoveReferences(IRInstruction instruction)
        {
            foreach (IRValueInstruction operand in instruction.Operands)
            {
                operand.References.Remove(instruction);

                RecursiveRemoveReferences(operand);
            }

            instruction.Operands.Clear();
        }

        /// <summary>
        /// Removes unused locals from functions and refragments its local repository
        /// </summary>
        void RemoveUnusedLocals()
        {
            foreach (var function in Module.Functions)
            {
                function.Locals.RemoveAll(l => l.References.Count == 0);
            }
        }

        /// <summary>
        /// Removes unused globals from the module and refragments its global repository
        /// </summary>
        void RemoveUnusedGlobals()
        {
            this.Module.Globals.RemoveAll(g => g.References.Count == 0);
        }
    }
}
