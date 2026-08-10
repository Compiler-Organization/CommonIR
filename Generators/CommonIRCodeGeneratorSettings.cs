using CommonIR.Passes.Optimization;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators
{
    public class CommonIRCodeGeneratorSettings
    {
        /// <summary>
        /// The target platform of the IR
        /// </summary>
        public CommonIRTargets Target { get; set; }

        /// <summary>
        /// Specifies the optimization level applied to the IR before generation.
        /// <para>Defaults to none</para>
        /// </summary>
        public OptimizingMode OptimizingMode { get; set; } = OptimizingMode.None;
    }
}
