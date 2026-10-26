using System;
using System.Collections.Generic;
using System.Text;
using Wasmtime;

namespace CommonIR.App
{
    internal class WasmBindings
    {
        public Memory Memory { get; set; }

        public WasmBindings(Memory memory)
        {
            this.Memory = memory;
        }

        public void ConsoleLog(int pointer, int length)
        {
            Console.WriteLine(this.Memory.ReadString(pointer, length, Encoding.UTF8));
        }

        public void ConsoleError(int pointer, int length)
        {
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(this.Memory.ReadString(pointer, length, Encoding.UTF8));
            Console.ForegroundColor = prev;
        }
    }
}
