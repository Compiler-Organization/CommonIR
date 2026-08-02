namespace CommonIR
{
    public class SourceFile
    {
        public string Name { get; set; }

        public string Extension { get; set; }

        public byte[] Data { get; set; }

        public SourceFile(string name, string extension, byte[] data)
        {
            Name = name;
            Extension = extension;
            Data = data;
        }

        public void WriteToDisk()
        {
            File.WriteAllBytes($"{Name}{Extension}", Data);
        }
    }
}
