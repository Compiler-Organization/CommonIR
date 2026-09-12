using CommonIR.Generators.Binary.CIL;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.Binary.CIL.Metadata
{
    internal class CILRuntimeconfigGenerator
    {
        public static string CreateConfig(CommonIRCILConfiguration cilConfiguration)
        {
            return $@"{{
                ""runtimeOptions"": {{
                    ""tfm"": ""{cilConfiguration.TargetFrameworkMoniker}"",
                    ""framework"": {{
                        ""name"": ""{cilConfiguration.FrameworkName}"",
                        ""version"": ""{cilConfiguration.FrameworkVersion}""
                    }}
                }}
            }}";
        }
    }
}
