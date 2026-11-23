using CommonIR.Errors;
using CommonIR.Generators.Binary.CIL;
using CommonIR.Generators.Binary.X8664;
using CommonIR.Generators.Textual.HLSL;
using CommonIR.Passes.Optimization;
using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR.Generators
{
    public class CommonIRCodeGeneratorSettings
    {
        /// <summary>
        /// The target platform of the IR
        /// </summary>
        public CommonIRTargets Target { get; set; }

        private CommonIRTargetConfiguration? _TargetConfiguration;

        /// <summary>
        /// Configure the specifics of targets
        /// </summary>
        public CommonIRTargetConfiguration? TargetConfiguration 
        { 
            get
            {
                if(_TargetConfiguration != null)
                {
                    return _TargetConfiguration;
                }

                switch(Target)
                {
                    case CommonIRTargets.CommonIntermediateLanguage:
                        {
                            _TargetConfiguration = new CommonIRCILConfiguration();
                            break;
                        }

                    case CommonIRTargets.WebAssembly:
                        {
                            _TargetConfiguration = new CommonIRTargetConfiguration();
                            break;
                        }

                    case CommonIRTargets.HLSL:
                        {
                            _TargetConfiguration = new CommonIRHLSLConfiguration();
                            break;
                        }

                    case CommonIRTargets.X8664:
                        {
                            _TargetConfiguration = new CommonIRX8664Configuration();
                            break;
                        }

                    case CommonIRTargets.Xtensa:
                        {
                            _TargetConfiguration = new Binary.Xtensa.CommonIRXtensaConfiguration();
                            break;
                        }

                    default:
                        {
                            throw ErrorHandler.Create($"Could not automatically generate a configuration for target '{Target}'");
                        }
                }

                return _TargetConfiguration;
            }
            set 
            { 
                _TargetConfiguration = value;
            }
        }

        /// <summary>
        /// Specifies the optimization level applied to the IR before generation.
        /// <para>Defaults to none</para>
        /// </summary>
        public OptimizingMode OptimizingMode { get; set; } = OptimizingMode.None;
    }
}
