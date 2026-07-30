using System;
using System.Collections.Generic;
using System.Text;

namespace CommonIR
{
    public class SourceFile
    {
        public required string Extension { get; set; }

        public required byte[] Data { get; set; }
    }
}
