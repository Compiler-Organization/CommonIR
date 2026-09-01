using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar
{
    public class IRRegister : IRGrammar
    {
        public int Id { get; set; }
        public IRType Type { get; set; }

        public bool IsVirtual { get; set; }

        public IRRegister(int id, IRType type)
        {
            this.Id = id;
            this.Type = type;
        }
    }
}
