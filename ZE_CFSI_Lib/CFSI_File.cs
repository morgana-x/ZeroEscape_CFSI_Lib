namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public int Size { get; set; }
        public int UnknownData { get; set; }
        public long FileOffset { get; set; } = 0;

        public CFSI_File(string name, string path, int unknownData, int size)
        {
            this.Path = path;
            this.Name = name;
            this.UnknownData = unknownData;
            this.Size = size;
        }
    }
}
