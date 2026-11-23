using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.ControlFlow;
using CommonIR.IR.Grammar.Objects;

namespace CommonIR.Passes.Optimization
{
    public class IRDeadCodeEliminator : IRPass
    {
        IRModule Module { get; set; }

        public IRDeadCodeEliminator(IRModule module)
        {
            this.Module = module;
        }

        public bool Pass()
        {
            HashSet<IRFunction> reachable = new HashSet<IRFunction>(ReferenceEqualityComparer.Instance);
            Stack<IRFunction> pending = new Stack<IRFunction>();

            void Reach(IRFunction function)
            {
                if (function is not IRFunctionImport && reachable.Add(function))
                {
                    pending.Push(function);
                }
            }

            foreach (IRFunction function in this.Module.Functions.Where(f => f.IsExport || f.IsKernel || ReferenceEquals(f, this.Module.EntryPoint)))
            {
                Reach(function);
            }

            foreach (IRGlobal global in this.Module.Globals)
            {
                foreach (IRFunction referenced in ReferencedFunctions(global.InitialValue))
                {
                    Reach(referenced);
                }
            }

            while (pending.Count > 0)
            {
                IRFunction function = pending.Pop();

                if (!new IRFunctionAnalysis(function).IsSupported)
                {
                    return false;
                }

                foreach (IRFunction referenced in ReferencedFunctions(function.Entryblock))
                {
                    Reach(referenced);
                }
            }

            List<IRFunction> unreachable = this.Module.Functions.Where(f => !reachable.Contains(f)).ToList();

            foreach (IRFunction function in unreachable)
            {
                IRFunctionAnalysis analysis = new IRFunctionAnalysis(function);

                foreach (IRInstruction statement in function.Entryblock.Instructions)
                {
                    IRTree.ReleaseStatement(statement, analysis);
                }

                this.Module.Functions.Remove(function);
            }

            return unreachable.Count > 0;
        }

        static IEnumerable<IRFunction> ReferencedFunctions(IRBlock block)
            => block.Instructions.SelectMany(ReferencedFunctions);

        static IEnumerable<IRFunction> ReferencedFunctions(IRInstruction instruction)
        {
            IRFunction? direct = instruction switch
            {
                IRCall call => call.Function,
                IRFunctionReference reference => reference.Function,
                IRDispatch dispatch => dispatch.Kernel,
                _ => null,
            };

            if (direct != null)
            {
                yield return direct;
            }

            foreach (IRValueInstruction child in IRTree.Children(instruction))
            {
                foreach (IRFunction function in ReferencedFunctions(child))
                {
                    yield return function;
                }
            }

            foreach (IRBlock block in IRTree.NestedBlocks(instruction))
            {
                foreach (IRFunction function in ReferencedFunctions(block))
                {
                    yield return function;
                }
            }
        }
    }
}
