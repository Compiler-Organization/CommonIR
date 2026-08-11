using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Arithmetic;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Instructions.Memory;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR
{
    public class IRBuilder
    {
        private IRModule Module { get; set; }

        /// <summary>
        /// The current function being built
        /// </summary>
        public IRFunction? Function { get; set; }

        /// <summary>
        /// The current block being built
        /// </summary>
        public IRBlock? Block { get; set; }

        private int Position { get; set; }

        public IRBuilder(IRModule module)
        {
            this.Module = module;
            this.Function = null;
            this.Block = null;
        }

        public IRBuilder(IRModule module, IRFunction function, IRBlock block)
        {
            this.Module = module;
            this.Function = function;
            this.Block = block;
        }

        private IRInstruction InsertInstruction(IRInstruction instruction)
        {
            if(this.Block == null)
            {
                throw ErrorHandler.Create("No block has been set in the IR builder!");
            }

            instruction.Parent = this.Block;

            this.Block.Instructions.Insert(this.Position++, instruction);
            return instruction;
        }

        private IRVoidInstruction InsertVoidInstruction(IRVoidInstruction instruction)
        {
            if (this.Block == null)
            {
                throw ErrorHandler.Create("No block has been set in the IR builder!");
            }

            instruction.Parent = this.Block;

            this.Block.Instructions.Insert(this.Position++, instruction);
            return instruction;
        }

        IRFunction? CheckpointFunction { get; set; }
        IRBlock? CheckpointBlock { get; set; }

        int CheckpointPosition { get; set; } = -1;

        /// <summary>
        /// Sets a checkpoint at the current function, in the current block at the current position
        /// </summary>
        public void SetCheckpoint()
        {
            this.CheckpointFunction = this.Function;
            this.CheckpointBlock = this.Block;
            this.CheckpointPosition = this.Position;
        }

        /// <summary>
        /// Restores the builder to the checkpoint at the function, in the block at its position.
        /// </summary>
        public void RestoreCheckpoint()
        {
            if (this.CheckpointFunction == null || this.CheckpointBlock == null || this.Position == -1)
            {
                throw ErrorHandler.Create($"Cannot restore checkpoint as no checkpoint has been set yet.");
            }

            this.Function = this.CheckpointFunction;
            this.Block = this.CheckpointBlock;
            this.Position = this.CheckpointPosition;
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
            if (this.Block == null)
            {
                throw ErrorHandler.Create("No block has been set in the IR builder!");
            }

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
        public void PositionAfterInstruction(IRInstruction instruction)
        {
            if (this.Block == null)
            {
                throw ErrorHandler.Create("No block has been set in the IR builder!");
            }

            int index = this.Block.Instructions.IndexOf(instruction);

            if (index == -1)
            {
                throw ErrorHandler.Create($"This instruction does not exist in the block '{this.Block.Name}'");
            }

            this.Position = index + 1;
        }

        /// <summary>
        /// Positions the IR builder at a given instruction in the current block.
        /// </summary>
        /// <param name="instruction"></param>
        public void PositionBeforeInstruction(IRInstruction instruction)
        {
            if (this.Block == null)
            {
                throw ErrorHandler.Create("No block has been set in the IR builder!");
            }

            int index = this.Block.Instructions.IndexOf(instruction);

            if (index == -1)
            {
                throw ErrorHandler.Create($"This instruction does not exist in the block '{this.Block.Name}'");
            }

            this.Position = index;
        }

        /// <summary>
        /// Builds a call instruction depending on if the function being called has a void return type or not.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public IRInstruction BuildCall(IRFunction function, List<IRValueInstruction> arguments)
        {
            return InsertInstruction(new IRCall(function, arguments));
        }

        /// <summary>
        /// Builds a addition instruction that adds two values together.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public IRValueInstruction BuildAdd(IRValueInstruction left, IRValueInstruction right)
        {
            IRValueInstruction instruction = new IRAdd(left: left, right: right);
            return instruction;
        }

        /// <summary>
        /// Builds a subtraction instruction which subtracts the right value from the left value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public IRValueInstruction BuildSubtract(IRValueInstruction left, IRValueInstruction right)
        {
            IRValueInstruction instruction = new IRSubtract(left: left, right: right);
            return instruction;
        }

        /// <summary>
        /// Builds a multiplication instruction which multiplies two values together.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public IRValueInstruction BuildMultiply(IRValueInstruction left, IRValueInstruction right)
        {
            IRValueInstruction instruction = new IRMultiply(left: left, right: right);
            return instruction;
        }

        /// <summary>
        /// Builds a division instruction which divides the left value by the right value.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public IRValueInstruction BuildDivide(IRValueInstruction left, IRValueInstruction right)
        {
            IRValueInstruction instruction = new IRDivide(left: left, right: right);
            return instruction;
        }

        /// <summary>
        /// Builds a constant integer of a specified integer type.
        /// </summary>
        /// <param name="integerType"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRValueInstruction BuildConstantInteger(IRDataTypes integerType, long value)
        {
            IRValueInstruction instruction = new IRConstantInteger(integerType: integerType, value: value);
            return instruction;
        }

        /// <summary>
        /// Builds a return instruction with no return value.
        /// </summary>
        /// <returns></returns>
        public IRVoidInstruction BuildReturn()
        {
            IRReturn returnInstruction = new IRReturn();
            InsertVoidInstruction(returnInstruction);
            return returnInstruction;
        }

        /// <summary>
        /// Builds a return instruction with a return value.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRVoidInstruction BuildReturn(IRValueInstruction value)
        {
            IRReturn returnInstruction = new IRReturn(value);
            InsertVoidInstruction(returnInstruction);
            return returnInstruction;
        }

        /// <summary>
        /// Builds a load to a given target value instruction.
        /// <para>If the target is an IRObject (such as IRLocal, IRGlobal), data will be loaded from their respective locations. If an integer is specified (Such as IRConstantInteger), this will be treated as a memory address and a value will be loaded from that address in memory.</para>
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public IRValueInstruction BuildLoad(IRValueInstruction target)
        {
            IRValueInstruction load = new IRLoad(target);
            return load;
        }

        /// <summary>
        /// Builds a store to a given target with the given value.
        /// <para>If the target is an IRObject (such as IRLocal, IRGlobal), data will be stored to their respective locations. If an integer is specified (Such as IRConstantInteger), this will be treated as a memory address and the value will be stored</para>
        /// </summary>
        /// <param name="target"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRVoidInstruction BuildStore(IRValueInstruction target, IRValueInstruction value)
        {
            IRVoidInstruction store = new IRStore(target, value);
            InsertVoidInstruction(store);
            return store;
        }

        /// <summary>
        /// Inserts a condiitonal branch which executes the thenBlock if the condition is met, else executes the elseBlock.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="thenBlock"></param>
        /// <param name="elseBlock"></param>
        /// <returns></returns>
        public IRVoidInstruction BuildConditionalBranch(IRValueInstruction condition, IRBlock thenBlock, IRBlock elseBlock)
        {
            IRVoidInstruction conditionalBranch = new IRConditionalBranch(condition, thenBlock, elseBlock);
            InsertVoidInstruction(conditionalBranch);
            return conditionalBranch;
        }

        /// <summary>
        /// Inserts a condiitonal branch which executes the thenBlock if the condition is met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="thenBlock"></param>
        /// <param name="elseBlock"></param>
        /// <returns></returns>
        public IRVoidInstruction BuildConditionalBranch(IRValueInstruction condition, IRBlock thenBlock)
        {
            IRVoidInstruction conditionalBranch = new IRConditionalBranch(condition, thenBlock);
            InsertVoidInstruction(conditionalBranch);
            return conditionalBranch;
        }

        /// <summary>
        /// Builds a comparsion between two values given the operator.
        /// </summary>
        /// <param name="comparisonOperator"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public IRValueInstruction BuildCompare(IRComparisonOperator comparisonOperator, IRValueInstruction left, IRValueInstruction right)
        {
            IRValueInstruction compare = new IRCompare(comparisonOperator, left, right);
            return compare;
        }

        /// <summary>
        /// Builds a string which returns the pointer to the string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRValueInstruction BuildString(string value)
        {
            IRValueInstruction _string = this.Module.CreateString(value);
            return _string;
        }
    }
}
