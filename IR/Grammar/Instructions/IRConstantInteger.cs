using CommonIR.IR.Grammar.Objects;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRConstantInteger : IRValueInstruction
    {
        public List<IRInstruction> References { get; set; } = new List<IRInstruction>();
        public bool IsVoid { get; } = false;

        public IRGrammar? Parent { get; set; }
        public IRDataTypes IntegerType { get; set; }
        public long Value { get; set; }
        public IRType ValueType { get; set; }

        public IRConstantInteger(IRDataTypes integerType, long value)
        {


            this.IntegerType = integerType;
            this.Value = value;

            this.ValueType = new IRType(integerType);
        }

        public string Dump()
        {
            return $"const ({this.IntegerType} {this.Value})";
        }
    }
}
