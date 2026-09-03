using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace CommonIR.Generators.CIL.Translation
{
    internal class CILTypeTranslator
    {
        public static IRModule? Module { get; set; }

        static Dictionary<IRStruct, Type> StructCache = [];

        public static Type TranslateIRDataType(IRDataTypes dataType)
            => dataType switch
            {
                IRDataTypes.Void => Type.GetType("System.Void, System.Runtime")!,
                IRDataTypes.Bool => Type.GetType("System.Boolean, System.Runtime")!,
                IRDataTypes.Int8 => Type.GetType("System.SByte, System.Runtime")!,
                IRDataTypes.UInt8 => Type.GetType("System.Byte, System.Runtime")!,
                IRDataTypes.Int16 => Type.GetType("System.Int16, System.Runtime")!,
                IRDataTypes.UInt16 => Type.GetType("System.UInt16, System.Runtime")!,
                IRDataTypes.Int32 => Type.GetType("System.Int32, System.Runtime")!,
                IRDataTypes.UInt32 => Type.GetType("System.UInt32, System.Runtime")!,
                IRDataTypes.Int64 => Type.GetType("System.Int64, System.Runtime")!,
                IRDataTypes.UInt64 => Type.GetType("System.UInt64, System.Runtime")!,
                IRDataTypes.String => Type.GetType("System.String, System.Runtime")!,
                _ => throw ErrorHandler.Create($"Translating datatype '{dataType}' to CIL is not supported")
            };

        public static Type TranslateIRType(IRType type)
        {
            if(type.DataType == IRDataTypes.Struct)
            {
                if (type.UserObject is not IRStruct irStruct)
                    throw ErrorHandler.Create($"Translating datatype '{type}' to CIL is not supported");

                return TranslateIRStruct(irStruct);
            }


            return TranslateIRDataType(type.DataType);
        }

        public static Type TranslateIRStruct(IRStruct irStruct)
        {
            if (StructCache.TryGetValue(irStruct, out Type? cachedType))
            {
                return cachedType;
            }
;
            TypeBuilder typeBuilder = Module!.CILModule!.DefineType(irStruct.Name, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout, Type.GetType("System.ValueType, System.Runtime"));

            foreach (IRStructProperty property in irStruct.Properties)
            {
                property.CILField = typeBuilder.DefineField(property.Name, TranslateIRType(property.ValueType), FieldAttributes.Public);
            }

            Type builtType = typeBuilder.CreateType();
            StructCache.Add(irStruct, builtType);

            return builtType;
        }

        public static Type TranslateIRTypes(List<IRType> types)
        {
            if (types.Count == 0) return TranslateIRDataType(IRDataTypes.Void);
            if (types.Count == 1) return TranslateIRType(types[0]);

            return CreateValueTupleType(types.Select(TranslateIRType).ToArray());
        }

        private static Type CreateValueTupleType(Type[] types)
        {
            Type openTupleType = types.Length switch
            {
                2 => typeof(ValueTuple<,>),
                3 => typeof(ValueTuple<,,>),
                4 => typeof(ValueTuple<,,,>),
                5 => typeof(ValueTuple<,,,,>),
                6 => typeof(ValueTuple<,,,,,>),
                7 => typeof(ValueTuple<,,,,,,>),
                _ => typeof(ValueTuple<,,,,,,,>)
            };

            if (types.Length <= 7)
            {
                return openTupleType.MakeGenericType(types);
            }

            Type[] firstSeven = types.Take(7).ToArray();
            Type[] rest = types.Skip(7).ToArray();

            Type[] genericArgs = new Type[8];
            firstSeven.CopyTo(genericArgs, 0);
            genericArgs[7] = CreateValueTupleType(rest);

            return openTupleType.MakeGenericType(genericArgs);
        }

        public static ConstructorInfo? CreateValueTupleTypeConstructor(Type[] types)
        {
            Type openTupleType = types.Length switch
            {
                2 => typeof(ValueTuple<,>),
                3 => typeof(ValueTuple<,,>),
                4 => typeof(ValueTuple<,,,>),
                5 => typeof(ValueTuple<,,,,>),
                6 => typeof(ValueTuple<,,,,,>),
                7 => typeof(ValueTuple<,,,,,,>),
                _ => typeof(ValueTuple<,,,,,,,>)
            };

            if (types.Length <= 7)
            {
                return openTupleType.MakeGenericType(types).GetConstructor(types);
            }

            Type[] firstSeven = types.Take(7).ToArray();
            Type[] rest = types.Skip(7).ToArray();

            Type[] genericArgs = new Type[8];
            firstSeven.CopyTo(genericArgs, 0);
            genericArgs[7] = CreateValueTupleType(rest);

            return openTupleType.MakeGenericType(genericArgs).GetConstructor(genericArgs);
        }
    }
}
