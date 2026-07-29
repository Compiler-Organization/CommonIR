using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.IR.Grammar.Objects
{
    public class IRModule
    {
        public List<IRGlobal> Globals { get; set; }

        public List<IRFunction> Functions { get; set; }

        public IRModule() 
        {
            this.Globals = new List<IRGlobal>();
            this.Functions = new List<IRFunction>();
        }
    }
}
