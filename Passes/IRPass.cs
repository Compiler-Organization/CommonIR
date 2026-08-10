using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Passes
{
    public interface IRPass
    {
        public void Pass();
    }
}
