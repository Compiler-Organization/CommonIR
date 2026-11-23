using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Passes.Optimization
{
    public class IROptimizer
    {
        private readonly IRModule Module;

        public const int MaximumRounds = 256;

        public int Rounds { get; private set; }

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
            int limit = optimizingMode == OptimizingMode.AggressiveExhaustive ? int.MaxValue : MaximumRounds;

            this.Rounds = 0;

            if (pipeline.Count > 0)
            {
                OptimizeUntilStable(pipeline, limit);
            }
        }

        private void OptimizeUntilStable(List<IRPass> pipeline, int limit)
        {
            bool changed = false;

            foreach (IRPass pass in pipeline)
            {
                changed |= pass.Pass();
            }

            this.Rounds++;

            if (changed && this.Rounds < limit)
            {
                OptimizeUntilStable(pipeline, limit);
            }
        }

        private List<IRPass> BuildPipeline(OptimizingMode mode)
        {
            List<IRPass> pipeline = new List<IRPass>();

            switch (mode)
            {
                case OptimizingMode.Moderate:
                    pipeline.Add(new IRExpressionSimplifier(Module));
                    pipeline.Add(new IRControlFlowSimplifier(Module));
                    break;

                case OptimizingMode.Aggressive:
                case OptimizingMode.AggressiveExhaustive:
                    pipeline.Add(new IRExpressionSimplifier(Module));
                    pipeline.Add(new IRControlFlowSimplifier(Module));
                    pipeline.Add(new IRLocalSimplifier(Module));
                    pipeline.Add(new IRLoopInvariantMover(Module));
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
        /// Repeats the following until nothing changes:
        /// <list type="bullet">
        /// <item>Constant folding, with the exact integer and floating point semantics of the targets</item>
        /// <item>Algebraic simplification (x + 0, x * 1, x - x, x &amp; x, ...)</item>
        /// <item>Strength reduction (multiplications and unsigned divisions by powers of two become shifts, floating point divisions by powers of two become multiplications)</item>
        /// <item>Merging chains of constant operations ((x + 1) + 2 becomes x + 3)</item>
        /// <item>Comparison and conversion simplification</item>
        /// <item>Removing branches on constant conditions, loops that never run, unreachable code and empty branches</item>
        /// </list>
        /// </summary>
        Moderate = 1,

        /// <summary>
        /// Repeats everything 'Moderate (1)' does, together with the following, until nothing changes:
        /// <list type="bullet">
        /// <item>Constant propagation of locals that are assigned a constant once</item>
        /// <item>Removal of assignments to locals that are never read</item>
        /// <item>Removal of functions that can not be reached</item>
        /// </list>
        /// </summary>
        Aggressive = 2,

        /// <summary>
        /// Performs the same optimizations as 'Aggressive (2)' with no limit on the number of rounds,
        /// which may result in extremely long compilation times for very large modules.
        /// </summary>
        AggressiveExhaustive = 3,
    }
}
