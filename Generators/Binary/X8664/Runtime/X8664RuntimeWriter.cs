using CommonIR.IR;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Instructions;
using CommonIR.IR.Grammar.Instructions.Numeric;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.X8664.Runtime
{
    internal class X8664RuntimeWriter
    {
        public IRModule Module { get; }

        public IRFunction Function { get; }

        public IRBuilder Builder { get; }

        Dictionary<string, IRFunctionImport> Imports { get; }

        public X8664RuntimeWriter(IRModule module, IRFunction function, Dictionary<string, IRFunctionImport> imports)
        {
            this.Module = module;
            this.Function = function;
            this.Imports = imports;
            this.Builder = new IRBuilder(module, function, function.Entryblock);
            this.Builder.PositionAtEnd(function, function.Entryblock);
        }

        public static IRType Type(IRDataTypes dataType) => new IRType(dataType);

        public IRValueInstruction Int32(long value) => this.Builder.BuildConstantInteger(IRDataTypes.Int32, value);

        public IRValueInstruction UInt32(long value) => this.Builder.BuildConstantInteger(IRDataTypes.UInt32, value);

        public IRValueInstruction Int64(long value) => this.Builder.BuildConstantInteger(IRDataTypes.Int64, value);

        public IRValueInstruction UInt64(long value) => this.Builder.BuildConstantInteger(IRDataTypes.UInt64, value);

        public IRValueInstruction Pointer(long value) => this.Builder.BuildConstantInteger(IRDataTypes.Pointer, value);

        public IRValueInstruction Bool(bool value) => this.Builder.BuildConstantInteger(IRDataTypes.Bool, value ? 1 : 0);

        public IRLocal Local(string name, IRType type) => this.Function.CreateLocal(name, type, isMutable: true);

        public IRLocal Local(string name, IRType type, IRValueInstruction initialValue)
        {
            IRLocal local = Local(name, type);
            Set(local, initialValue);
            return local;
        }

        public IRLocal Parameter(int index) => this.Function.Parameters[index];

        public void Set(IRValueInstruction target, IRValueInstruction value) => this.Builder.BuildStore(target, value);

        public IRValueInstruction Read(IRValueInstruction pointer, int offset, IRDataTypes dataType) => this.Builder.BuildLoad(pointer, Type(dataType), Int32(offset));

        public IRValueInstruction Read(IRValueInstruction pointer, int offset, IRType type) => this.Builder.BuildLoad(pointer, type, Int32(offset));

        public IRValueInstruction ReadAt(IRValueInstruction pointer, IRValueInstruction offset, IRDataTypes dataType) => this.Builder.BuildLoad(pointer, Type(dataType), offset);

        public void Write(IRValueInstruction pointer, int offset, IRValueInstruction value) => this.Builder.BuildStore(pointer, Int32(offset), value);

        public void WriteAt(IRValueInstruction pointer, IRValueInstruction offset, IRValueInstruction value) => this.Builder.BuildStore(pointer, offset, value);

        public IRValueInstruction Add(IRValueInstruction left, IRValueInstruction right) => this.Builder.BuildAdd(left, right);

        public IRValueInstruction Subtract(IRValueInstruction left, IRValueInstruction right) => this.Builder.BuildSubtract(left, right);

        public IRValueInstruction Multiply(IRValueInstruction left, IRValueInstruction right) => this.Builder.BuildMultiply(left, right);

        public IRValueInstruction Divide(IRValueInstruction left, IRValueInstruction right) => this.Builder.BuildDivide(left, right);

        public IRValueInstruction Compare(IRComparisonOperator comparisonOperator, IRValueInstruction left, IRValueInstruction right) => this.Builder.BuildCompare(comparisonOperator, left, right);

        public IRValueInstruction Convert(IRValueInstruction value, IRDataTypes dataType) => this.Builder.BuildConvert(value, Type(dataType));

        public IRValueInstruction Convert(IRValueInstruction value, IRType type) => this.Builder.BuildConvert(value, type);

        public IRValueInstruction DataPointer(IRValueInstruction fatPointer) => Convert(fatPointer, IRDataTypes.Pointer);

        public IRValueInstruction Length(IRValueInstruction fatPointer) => this.Builder.BuildLength(fatPointer);

        public IRValueInstruction FatPointer(IRValueInstruction pointer, IRValueInstruction length, IRType type) => this.Builder.BuildFatPointer(pointer, length, type);

        public IRValueInstruction Call(IRFunction function, params IRValueInstruction[] arguments) => (IRValueInstruction)this.Builder.BuildCall(function, arguments.ToList());

        public void Invoke(IRFunction function, params IRValueInstruction[] arguments) => this.Builder.BuildCall(function, arguments.ToList());

        public IRFunctionImport Import(string moduleName, string functionName, IRDataTypes returnType, params IRDataTypes[] parameterTypes)
        {
            string key = $"{moduleName.ToLowerInvariant()}!{functionName}";

            if (!this.Imports.TryGetValue(key, out IRFunctionImport? functionImport))
            {
                functionImport = this.Module.CreateFunctionImport(moduleName, functionName, Type(returnType), parameterTypes.Select((p, i) => new IRLocal($"p{i}", p, isMutable: false)).ToList());
                this.Imports[key] = functionImport;
            }

            return functionImport;
        }

        public IRValueInstruction Native(string moduleName, string functionName, IRDataTypes returnType, params IRValueInstruction[] arguments)
        {
            IRFunctionImport functionImport = Import(moduleName, functionName, returnType, arguments.Select(a => a.ValueType.DataType).ToArray());
            return (IRValueInstruction)this.Builder.BuildCall(functionImport, arguments.ToList());
        }

        public IRValueInstruction Com(IRValueInstruction instance, int method, IRDataTypes returnType, params IRValueInstruction[] arguments)
        {
            IRValueInstruction table = Read(instance, 0, IRDataTypes.Pointer);
            IRValueInstruction entry = Read(table, method * 8, IRDataTypes.Pointer);

            return (IRValueInstruction)this.Builder.BuildCallIndirect(entry, Type(returnType), [instance, .. arguments]);
        }

        public void If(IRValueInstruction condition, Action then, Action? otherwise = null)
        {
            IRBlock thenBlock = this.Function.CreateBlock("then");
            IRBlock? elseBlock = otherwise != null ? this.Function.CreateBlock("else") : null;

            this.Builder.SetCheckpoint();

            this.Builder.PositionAtStart(this.Function, thenBlock);
            then();

            if (elseBlock != null)
            {
                this.Builder.PositionAtStart(this.Function, elseBlock);
                otherwise!();
            }

            this.Builder.RestoreCheckpoint();

            if (elseBlock != null)
            {
                this.Builder.BuildConditionalBranch(condition, thenBlock, elseBlock);
            }
            else
            {
                this.Builder.BuildConditionalBranch(condition, thenBlock);
            }
        }

        public void While(IRValueInstruction condition, Action body)
        {
            IRBlock loopBlock = this.Function.CreateBlock("loop");

            this.Builder.SetCheckpoint();
            this.Builder.PositionAtStart(this.Function, loopBlock);
            body();
            this.Builder.RestoreCheckpoint();

            this.Builder.BuildLoop(condition, loopBlock);
        }

        public void Return() => this.Builder.BuildReturn();

        public void Return(IRValueInstruction value) => this.Builder.BuildReturn(value);
    }
}
