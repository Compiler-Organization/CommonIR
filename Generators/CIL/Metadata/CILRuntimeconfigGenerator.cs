using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.CIL.Metadata
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
