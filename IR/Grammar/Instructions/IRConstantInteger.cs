using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRConstantInteger : IRInstruction, IRValueInstruction
    {
        public IRDataTypes IntegerType { get; set; }
        public long Value { get; set; }
        public IRType Type { get; set; }

        public IRConstantInteger(IRDataTypes integerType, long value)
        {
            this.IntegerType = integerType;
            this.Value = value;

            this.Type = new IRType(integerType);
        }
    }
}
