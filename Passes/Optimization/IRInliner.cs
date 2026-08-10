using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Passes.Optimization
{
    public class IRInliner : IRPass
    {
        IRModule Module { get; set; }

        public IRInliner(IRModule module)
        {
            this.Module = module;
        }

        public void Pass()
        {

        }

        void InlineFunctionCalls()
        {
            foreach(IRFunction function in this.Module.Functions)
            {

            }
        }
    }
}
