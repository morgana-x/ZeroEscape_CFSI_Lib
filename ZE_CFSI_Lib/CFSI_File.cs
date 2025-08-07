namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        public string Path { get; set; } // Full path (Within archive)
        public string Name { get; set; } // File name and extension
        public uint Size { get; set; }
        public uint Offset { get; set; } // Offset relative to Data section
        public bool Compressed { get; set; } = false;

        public CFSI_File(string name, string path, uint offset, uint size)
        {
            Path = path;
            Name = name;
            Offset = offset;
            Size = size;
        }
    }
}
