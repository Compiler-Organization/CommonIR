using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators.CIL
{
    public class CommonIRCILConfiguration : CommonIRTargetConfiguration
    {
        public string FrameworkVersion { get; set; } = "10.0.0";
        public string FrameworkName { get; set; } = "Microsoft.NETCore.App";

        /// <summary>
        /// Automatically generated using the configs default <see cref="FrameworkVersion"/>
        /// </summary>
        public string TargetFrameworkMoniker { get; set; }

        /// <summary>
        /// Automatically generated using the configs default <see cref="FrameworkVersion"/>
        /// </summary>
        public string FrameworkPath { get; set; }

        public string CoreAssemblyFullName { get; set; } = "System.Runtime";

        public CommonIRCILConfiguration()
        {
            FrameworkPath = $@"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\{FrameworkVersion}\ref\net{FrameworkVersion.Substring(0, 4)}";
            TargetFrameworkMoniker = $"net{FrameworkVersion.Substring(0, 4)}";
        }
    }
}
