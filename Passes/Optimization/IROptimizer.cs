using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Passes.Optimization
{
    public class IROptimizer
    {
        private readonly IRModule Module;

        public IROptimizer(IRModule module)
        {
            this.Module = module;
        }

        /// <summary>
        /// Optimizes the module using the pipeline of the specified optimization mode.
        /// </summary>
        public void Optimize(OptimizingMode optimizingMode)
        {
            List<IRPass> pipeline = BuildPipeline(optimizingMode);

            foreach (IRPass pass in pipeline)
            {
                pass.Pass();
            }
        }

        private List<IRPass> BuildPipeline(OptimizingMode mode)
        {
            List<IRPass> pipeline = new List<IRPass>();

            switch (mode)
            {
                case OptimizingMode.Moderate:
                    pipeline.Add(new IRDeadCodeEliminator(Module));
                    break;

                case OptimizingMode.Aggressive:
                    pipeline.Add(new IRDeadCodeEliminator(Module));
                    pipeline.Add(new IRDeadCodeEliminator(Module));
                    break;

                case OptimizingMode.None:
                default:
                    break;
            }

            return pipeline;
        }
    }

    public enum OptimizingMode
    {
        /// <summary>
        /// Performs absolutely no optimization.
        /// </summary>
        None = 0,

        /// <summary>
        /// Performs basic optimization as listed below:
        /// <list type="bullet">
        /// <item>Dead code elimination</item>
        /// <item>Constant folding</item>
        /// </list>
        /// </summary>
        Moderate = 1,

        /// <summary>
        /// Performs aggressive optimizations as listed below:
        /// <list type="bullet">
        /// <item>Dead code elimination</item>
        /// <item>Constant folding</item>
        /// </list>
        /// </summary>
        Aggressive = 2,

        /// <summary>
        /// Exhaustively optimizes every single facet of the IR which may result in extremely long compilation times.
        /// <para>Performs the same optimizations as 'Aggressive (2)'</para>
        /// </summary>
        AggressiveExhaustive = 3,
    }
}
