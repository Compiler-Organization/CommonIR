using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Instructions
{
    public class IRCodeBlock : IRInstruction
    {
        public bool IsVoid { get; } = true;

        public IRGrammar? Parent { get; set; }
        /// <summary>
        /// The name of the block. Automatically generated if not defined.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The type of the block. If not defined, the block is set to void.
        /// </summary>
        public IRType ReturnType { get; set; } = new IRType(IRDataTypes.Void);

        /// <summary>
        /// Instructions in the block.
        /// </summary>
        public List<IRInstruction> Instructions { get; set; } = new List<IRInstruction>();


        public string Dump()
        {
            string innerContent = string.Join("\n", Instructions.Select(i => i.Dump()));

            string indentedContent = string.Join("\n", innerContent
                .Split('\n')
                .Select(line => $"\t{line}"));

            return $"block %{Name} {{\n{indentedContent}\n}}";
        }
    }
}
