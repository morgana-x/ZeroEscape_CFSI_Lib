namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public int Size { get; set; }
        public int Offset { get; set; }
        public long FileOffset { get; set; } = 0;

        public bool Compressed { get; set; } = false;

        public int UncompressedSize { get; set; } = 0;

        public short GzipSign { get; set; } = 0;

        public CFSI_File(string name, string path, int unknownData, int size)
        {
            this.Path = path;
            this.Name = name;
            this.Offset = unknownData;
            this.Size = size;
        }
    }
}
