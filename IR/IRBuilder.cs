using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR
{
    public class IRBuilder
    {
        private IRModule Module { get; set; }

        private IRFunction Function { get; set; }

        private IRBlock Block { get; set; }

        private int Position { get; set; }

        public IRBuilder(IRModule module, IRFunction function, IRBlock block)
        {
            this.Module = module;
            this.Function = function;
            this.Block = block;
        }

        private void BuildInstruction(IRInstruction instruction)
        {
            this.Block.Instructions.Insert(this.Position, instruction);
        }

        /// <summary>
        /// Positions the IR builder at the start of the current block.
        /// </summary>
        public void PositionAtStart()
        {
            this.Position = 0;
        }

        /// <summary>
        /// Positions the IR builder at the start of a given block.
        /// </summary>
        /// <param name="block"></param>
        public void PositionAtStart(IRFunction function, IRBlock block)
        {
            this.Function = function;
            this.Block = block;
            this.Position = 0;
        }

        /// <summary>
        /// Positions the IR builder at the end of the current block.
        /// </summary>
        public void PositionAtEnd()
        {
            this.Position = this.Block.Instructions.Count;
        }

        /// <summary>
        /// Positions the IR builder at the end of a given block.
        /// </summary>
        /// <param name="block"></param>
        public void PositionAtEnd(IRFunction function, IRBlock block)
        {
            this.Function = function;
            this.Block = block;
            this.Position = block.Instructions.Count;
        }

        /// <summary>
        /// Positions the IR builder at a given index in the current block.
        /// </summary>
        /// <param name="index"></param>
        public void PositionAtIndex(int index)
        {
            this.Position = index;
        }

        /// <summary>
        /// Positions the IR builder at a given instruction in the current block.
        /// </summary>
        /// <param name="instruction"></param>
        public void PositionAtInstruction(IRInstruction instruction)
        {
            this.Position = this.Block.Instructions.IndexOf(instruction);

            if(this.Position == -1)
            {
                throw ErrorHandler.Create($"This instruction does not exist in the block '{this.Block.Name}'");
            }
        }

        /// <summary>
        /// Builds a local in the current block.
        /// </summary>
        /// <param name="local"></param>
        public void BuildLocal(IRLocal local)
        {
            this.Function.Locals.Add(local);
        }

        /// <summary>
        /// Builds a conditional branch that moves to the given block if the condition is met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="targetBlock"></param>
        public void BuildIfBranch(IRGrammar condition, IRBlock targetBlock)
        {
            if(condition is IRLocal local)
            {
                if(local.Type.DataType != IRDataTypes.Bool)
                {
                    throw ErrorHandler.Create($"Local '{local.Name}' of type '{local.Type.DataType.ToString()}' is not allowed in conditional branches.");
                }
            }

            if(condition is IRGlobal global)
            {
                if (global.Type.DataType != IRDataTypes.Bool)
                {
                    throw ErrorHandler.Create($"Local '{global.Name}' of type '{global.Type.DataType.ToString()}' is not allowed in conditional branches.");
                }
            }

            BuildInstruction(new IRBranchIf
            {
                Condition = condition,
                Block = targetBlock,
            });
        }

        public void BuildAdd(IRGrammar left, IRGrammar right)
        {

        }
    }
}
