using System.Text;
namespace ZE_CFSI_Lib
{
    internal class CFSI_Util
    {
        // http://aluigi.org/bms/zero_time_dilemma.bms
        // Whilst all other aspects of file were manually investigated without looking at the script,
        // the way of getting the padding of the offsets for files was sourced off Aluigi's BMS script
        // Credits to Aluigi for figuring that out!

        public static string Read_CFSI_String(Stream stream)
        {
            int stringLen = stream.ReadByte();
            return Encoding.ASCII.GetString(Read_ByteArray(stream, stringLen));
        }

        public static void Write_CFSI_String(Stream stream, string text)
        {

            byte[] bytes = Encoding.ASCII.GetBytes(text.Replace("\\", "/"));
            if (bytes.Length == 2 && bytes[1] == 47) // Skip writing empty string
            {
                stream.WriteByte(0);
                return;
            }
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes);
        }

        public static uint Read_CFSI_VINT(Stream stream)
        {
            uint value = (uint)stream.ReadByte();
            if (value == 0xFC)
                return (uint)Read_UInt16(stream);
            return value;
        }

        public static void Write_CFSI_VINT(Stream stream, ushort num)
        {
            if (num != 0xFC && num <= 0xFF)
            {
                stream.WriteByte((byte)num);
                return;
            }
            stream.WriteByte(0xFC);
            stream.Write(BitConverter.GetBytes(num));
        }


        private const int CFSI_Align = 0x10; // http://aluigi.org/bms/zero_time_dilemma.bms

        // https://en.wikipedia.org/wiki/Data_structure_alignment#Computing_padding
        internal static long CFSI_Get_Aligned(uint offset, int align = CFSI_Align)
        {
            return offset + CFSI_Get_Padding(offset, align);
        }
        internal static long CFSI_Get_Aligned(long offset, int align = CFSI_Align)
        {
            return offset + CFSI_Get_Padding(offset, align);
        }
        internal static long CFSI_Get_Padding(uint offset, int align = CFSI_Align)
        {
            return ((align - (offset % align)) % align);
        }
        internal static long CFSI_Get_Padding(long offset, int align = CFSI_Align)
        {
            return ((align - (offset % align)) % align);
        }
        public static string CFSI_Get_FolderPath(string sourceFilePath, string file)
        {
            string folderPath = file.Replace(sourceFilePath, "");
            if (folderPath.Contains("\\"))
                folderPath = folderPath.Substring(0, folderPath.LastIndexOf("\\"));
            else folderPath = ((char)(byte)(0)).ToString();
            if ((folderPath.EndsWith("\\") || folderPath.EndsWith("/")) && folderPath.Length > 1)
                folderPath = folderPath.Substring(0, folderPath.Length - 1);
            return folderPath;
        }

        private static string[] CompressedFileExtensions = new string[] { ".orb", ".uaz", ".rtz", ".bft", ".pfx" };
        public static bool CFSI_ShouldBeCompressed(string fileName)
        {
            for (int i = 0; i < CompressedFileExtensions.Length; i++)
                if (fileName.EndsWith(CompressedFileExtensions[i]))
                    return true;
            return false;
        }

        internal static Stream CFSI_Get_Compressed(Stream stream)
        {
            MemoryStream compressedMemoryStream = new MemoryStream();
            compressedMemoryStream.Write(BitConverter.GetBytes((uint)stream.Length));
            ICSharpCode.SharpZipLib.GZip.GZip.Compress(stream, compressedMemoryStream, false, level: 2, bufferSize:512);
            return compressedMemoryStream;
        }

        internal static Stream CFSI_Get_Compressed(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var newStream = CFSI_Get_Compressed(fs);
            fs.Dispose();
            fs.Close();
            return newStream;
        }

        internal static Stream CFSI_Pack_OpenFile(string file)
        {
            if (CFSI_Util.CFSI_ShouldBeCompressed(file))
                return CFSI_Util.CFSI_Get_Compressed(file);
            return File.OpenRead(file);
        }

        internal static uint Read_UInt32(Stream stream)
        {
            return BitConverter.ToUInt32(Read_ByteArray(stream, 4), 0);
        }

        internal static ushort Read_UInt16(Stream stream)
        {
            return BitConverter.ToUInt16(Read_ByteArray(stream, 2), 0);
        }

        internal static byte[] Read_ByteArray(Stream stream, int length = -1)
        {
            if (length < 0)
                length = (int)stream.Length;
            byte[] bytes = new byte[length];
            stream.Read(bytes);
            return bytes;
        }

    }
}
