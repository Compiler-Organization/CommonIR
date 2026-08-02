using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;

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

        private IRInstruction InsertInstruction(IRInstruction instruction)
        {
            this.Block.Instructions.Insert(this.Position, instruction);
            return instruction;
        }

        private IRValueInstruction InsertInstruction(IRValueInstruction instruction)
        {
            // this.Block.Instructions.Insert(this.Position, (IRInstruction)instruction);
            return instruction;
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

            if (this.Position == -1)
            {
                throw ErrorHandler.Create($"This instruction does not exist in the block '{this.Block.Name}'");
            }
        }

        /// <summary>
        /// Builds a local in the current block.
        /// </summary>
        /// <param name="local"></param>
        public void CreateLocal(IRLocal local)
        {
            this.Function.Locals.Add(local);
        }

        /// <summary>
        /// Builds a local in the current block.
        /// </summary>
        /// <param name="local"></param>
        public void CreateLocal(string name, IRType type, bool isMutable)
        {
            this.Function.Locals.Add(new IRLocal(name, type, isMutable));
        }

        /// <summary>
        /// Builds a conditional branch that moves to the given block if the condition is met.
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="targetBlock"></param>
        public IRInstruction BuildConditionalBranch(IRValueInstruction condition, IRBlock targetBlock)
        {
            IRInstruction conditionalBranch = new IRConditionalJump(condition, targetBlock);
            InsertInstruction(conditionalBranch);
            return conditionalBranch;
        }

        /// <summary>
        /// Builds a call instruction depending on if the function being called has a void return type or not.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public IRInstruction BuildCall(IRFunction function, List<IRValueInstruction> arguments)
        {
            if (function.ReturnTypes.Count == 0 || (function.ReturnTypes.Count == 1 && function.ReturnTypes[0].DataType == IRDataTypes.Void))
            {
                return InsertInstruction((IRInstruction)new IRCall(function, arguments));
            }
            else
            {
                return InsertInstruction(new IRCall(function, arguments));
            }
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
            InsertInstruction(instruction);
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
            InsertInstruction(instruction);
            return instruction;
        }

        /// <summary>
        /// Builds a return instruction with no return value.
        /// </summary>
        /// <returns></returns>
        public IRInstruction BuildReturn()
        {
            return new IRReturn();
        }

        /// <summary>
        /// Builds a return instruction with a return value.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public IRInstruction BuildReturn(IRValueInstruction value)
        {
            IRReturn returnInstruction = new IRReturn
            {
                Value = value
            };
            InsertInstruction(returnInstruction);
            return returnInstruction;
        }

        /// <summary>
        /// Builds a load to a given target value instruction.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public IRValueInstruction BuildLoad(IRValueInstruction target)
        {
            IRValueInstruction load = new IRLoad(target);
            InsertInstruction(load);
            return load;
        }
    }
}
