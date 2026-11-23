using CommonIR.IR.Grammar.Objects;
using System.Collections.Generic;
using System.Linq;

namespace CommonIR.Generators.Binary.Xtensa
{
    internal class XtensaSoftFloat
    {
        public IRFunction Add { get; }

        public IRFunction Subtract { get; }

        public IRFunction Multiply { get; }

        public IRFunction Divide { get; }

        public IRFunction Compare { get; }

        public IRFunction FromInt64 { get; }

        public IRFunction FromUInt64 { get; }

        public IRFunction ToInt64 { get; }

        public IRFunction ToUInt64 { get; }

        public IRFunction FromSingle { get; }

        public IRFunction ToSingle { get; }

        public IRFunction SquareRoot { get; }

        public IRFunction SquareRootSingle { get; }

        Dictionary<string, IRFunction> Math { get; } = new Dictionary<string, IRFunction>();

        XtensaSoftFloat(Dictionary<string, IRFunction> functions)
        {
            this.Add = functions["SoftFloat.Add"];
            this.Subtract = functions["SoftFloat.Subtract"];
            this.Multiply = functions["SoftFloat.Multiply"];
            this.Divide = functions["SoftFloat.Divide"];
            this.Compare = functions["SoftFloat.Compare"];
            this.FromInt64 = functions["SoftFloat.FromInt64"];
            this.FromUInt64 = functions["SoftFloat.FromUInt64"];
            this.ToInt64 = functions["SoftFloat.ToInt64"];
            this.ToUInt64 = functions["SoftFloat.ToUInt64"];
            this.FromSingle = functions["SoftFloat.FromSingle"];
            this.ToSingle = functions["SoftFloat.ToSingle"];
            this.SquareRoot = functions["SoftFloat.Sqrt"];
            this.SquareRootSingle = functions["SoftFloat.SqrtSingle"];

            foreach ((string name, string function) in new[]
            {
                ("sqrt", "Sqrt"), ("sin", "Sin"), ("cos", "Cos"), ("tan", "Tan"), ("exp", "Exp"), ("ln", "Ln"),
                ("floor", "Floor"), ("ceil", "Ceil"), ("round", "Round"), ("pow", "Pow"), ("atan2", "Atan2"),
            })
            {
                if (functions.TryGetValue($"SoftMath.{function}", out IRFunction? found))
                {
                    this.Math[name] = found;
                }
            }
        }

        public static XtensaSoftFloat? Find(IRModule module)
        {
            Dictionary<string, IRFunction> functions = new Dictionary<string, IRFunction>();

            foreach (IRFunction function in module.Functions)
            {
                functions.TryAdd(function.Name, function);
            }

            return functions.ContainsKey("SoftFloat.Add") ? new XtensaSoftFloat(functions) : null;
        }

        public IRFunction? Library(string name) => this.Math.GetValueOrDefault(name);

        public IEnumerable<IRFunction> All => new[] { this.Add, this.Subtract, this.Multiply, this.Divide, this.Compare, this.FromInt64, this.FromUInt64, this.ToInt64, this.ToUInt64, this.FromSingle, this.ToSingle, this.SquareRoot, this.SquareRootSingle };
    }
}
