namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        public string Path { get; set; } // Full path (Within archive)
        public string Name { get; set; } // File name and extension
        public long Size { get; set; }
        public long Offset { get; set; } // Offset relative to Data section
        public long FileOffset { get; set; } = 0; // Absolute offset in file
        public bool Compressed { get; set; } = false;

        public CFSI_File(string name, string path, long offset, long size)
        {
            Path = path;
            Name = name;
            Offset = offset;
            Size = size;
        }
    }
}
