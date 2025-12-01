namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        public string Path { get; set; } // Full path (Within archive)
        public string Name { get; set; } // File name and extension
        public uint Size { get; set; }
        public uint Offset { get; set; } // Offset relative to Data section
        public bool Compressed { get; set; } = false;

        public CFSI_File(Stream stream, string folder)
        {
            Name = CFSI_Util.Read_CFSI_String(stream);
            Offset = CFSI_Util.Read_UInt32(stream) * 16;
            Size = CFSI_Util.Read_UInt32(stream);

            Path = folder + Name;
        }
    }
}
