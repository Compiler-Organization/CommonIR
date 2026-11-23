namespace CommonIR.Generators.Binary.Xtensa
{
    public class CommonIRXtensaConfiguration : CommonIRTargetConfiguration
    {
        public uint IramBase { get; set; } = 0x40378000;

        public uint DramBase { get; set; } = 0x3FC88000;

        public uint LoadLimit { get; set; } = 0x3FCE9700;

        public uint StackTop { get; set; } = 0x3FCEB700;

        public uint StackSize { get; set; } = 0xC000;

        public uint StackLimit => this.StackTop - this.StackSize + 64;

        public uint HeapEnd => this.StackTop - this.StackSize;
    }
}
