using System.Text.Json.Serialization;

namespace ZE_CFSI_Lib
{
    public class CFSI_File
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("size")]
        public uint Size { get; set; }

        [JsonPropertyName("offset")]
        public uint Offset { get; set; }

        [JsonPropertyName("compressed")]
        public bool Compressed { get; set; } = false;

        [JsonPropertyName("originalHeaderBytes")]
        public byte[]? OriginalHeaderBytes { get; set; }

        [JsonPropertyName("originalHeaderPosition")]
        public long OriginalHeaderPosition { get; set; }

        [JsonPropertyName("originalOffsetValue")]
        public uint OriginalOffsetValue { get; set; }

        [JsonPropertyName("originalSizeValue")]
        public uint OriginalSizeValue { get; set; }

        public CFSI_File(string name, string path, uint offset, uint size)
        {
            Path = path;
            Name = name;
            Offset = offset;
            Size = size;
        }
    }
}
