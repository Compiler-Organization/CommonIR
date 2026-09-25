using CommonIR.Errors;
using CommonIR.IR.Grammar;
using CommonIR.IR.Grammar.Objects;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Textual.HLSL.Translation
{
    internal class HLSLTypeTranslator
    {
        /// <summary>
        /// Translates a struct schema to its declarative representation. Used for emitting cbuffers. Readonly structures.
        /// </summary>
        /// <param name="cbufferSchema"></param>
        /// <returns></returns>
        public static string ToHLSLCBuffer(IRStructSchema cbufferSchema)
        {
            if (cbufferSchema == null) throw new ArgumentNullException(nameof(cbufferSchema));

            var propertiesCode = string.Join("\n", cbufferSchema.Properties.Select(p => $"\t{ToHLSLCBufferProperty(p)}"));
            return $"cbuffer {cbufferSchema.Name} : register(b0)\n{{\n{propertiesCode}\n}};";
        }

        /// <summary>
        /// Translates a cbuffer property to its descriptive representation.
        /// </summary>
        /// <param name="cbufferProperty"></param>
        /// <returns></returns>
        public static string ToHLSLCBufferProperty(IRStructProperty cbufferProperty)
        {
            if (cbufferProperty == null) throw new ArgumentNullException(nameof(cbufferProperty));

            return $"{ToHLSLType(cbufferProperty.ValueType, false)} {cbufferProperty.Name};";
        }

        /// <summary>
        /// Translates an IRType to its declarative representation. Includes a trailing semicolon and semantic register.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="readAndWrite">If true, applies both read and write transformations. Otherwise readonly.</param>
        /// <returns></returns>
        public static string ToHLSLDeclaration(IRType type, string name, bool readAndWrite)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Variable name cannot be empty.", nameof(name));

            return $"{ToHLSLType(type, readAndWrite)} {name} : register(u0);";
        }

        /// <summary>
        /// Translates an IRType to its type representation. Supports direct translation of, for example, arrays to (RW)StructuredBuffer.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="readAndWrite">If true, applies both read and write transformations. Otherwise readonly.</param>
        /// <returns></returns>
        public static string ToHLSLType(IRType type, bool readAndWrite)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            return type.DataType switch
            {
                IRDataTypes.Int32 => "int",
                IRDataTypes.UInt32 => "uint",
                IRDataTypes.Float32 => "float",
                IRDataTypes.Float64 => "double",
                IRDataTypes.Bool => "bool",
                IRDataTypes.Void => "void",

                IRDataTypes.Struct => ((IRStructSchema)type.UserObject!).Name,

                IRDataTypes.Array => $"{(readAndWrite ? "RW" : "")}StructuredBuffer<{ToHLSLType(((IRArraySchema)type.UserObject!).ElementType, false)}>",

                _ => throw ErrorHandler.CreateNotImplimented($"Translation of IRType DataType '{type.DataType}' to HLSL type is not supported.")
            };
        }
    }
}
