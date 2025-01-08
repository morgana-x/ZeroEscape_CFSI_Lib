using System.IO.Compression;
using System.Text;
namespace ZE_CFSI_Lib
{
    internal class CFSI_Util
    {
        // http://aluigi.org/bms/zero_time_dilemma.bms
        // Whilst all other aspects of file were manually investigated without looking at the script,
        // the way of getting the padding of the offsets for files was sourced off Aluigi's BMS script
        // Credits to Aluigi for figuring that out!

        private static int CFSI_Align = 0x10; // http://aluigi.org/bms/zero_time_dilemma.bms
        public static string Read_CFSI_String(Stream stream)
        {
            int stringLen = stream.ReadByte();
            byte[] stringBuffer = new byte[stringLen];
            stream.Read(stringBuffer, 0, stringLen);

            return Encoding.ASCII.GetString(stringBuffer);
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
            if (value == 252)
                value = (uint)Read_UInt16(stream);
            return value;
        }
        public static void Write_CFSI_VINT(Stream stream, ushort num)
        {
            if (num < 252)
            {
                stream.WriteByte((byte)num);
                return;
            }
            stream.WriteByte(252);
            stream.Write(BitConverter.GetBytes(num));
        }
        // https://en.wikipedia.org/wiki/Data_structure_alignment#Computing_padding
        public static int CFSI_Get_Aligned(int offset)
        {
            return offset + CFSI_Get_Padding(offset);
        }
        public static long CFSI_Get_Aligned(long offset)
        {
            return offset + CFSI_Get_Padding(offset);
        }
        public static int CFSI_Get_Padding(int offset)
        {
            return ((CFSI_Align - (offset % CFSI_Align)) % CFSI_Align);
        }
        public static long CFSI_Get_Padding(long offset)
        {
            return ((CFSI_Align - (offset % CFSI_Align)) % CFSI_Align);
        }
        public static string CFSI_Get_FolderPath(string badString, string file)
        {
            string folderPath = file.Replace(badString, "");
            if (folderPath.Contains("\\"))
                folderPath = folderPath.Substring(0, folderPath.LastIndexOf("\\"));
            else folderPath = ((char)(byte)(0)).ToString();
            if ((folderPath.EndsWith("\\") || folderPath.EndsWith("/")) && folderPath.Length > 1)
                folderPath = folderPath.Substring(0, folderPath.Length - 1);
            return folderPath;
        }
        internal static bool CFSI_ShouldBeCompressed(string fileName)
        {
            if (fileName.EndsWith(".orb")) return true;
            if (fileName.EndsWith(".uaz")) return true;
            if (fileName.EndsWith(".rtz")) return true;
            //  if (fileName.EndsWith(".bin")) return true; // May not always be case test when repacking 00000000 is more complete
            return false;
        }
        internal static MemoryStream CFSI_GetCompressed(Stream stream)
        {
            MemoryStream compressedMemoryStream = new MemoryStream();
            var gstream = new GZipStream(compressedMemoryStream, CompressionLevel.Optimal);
            stream.CopyTo(compressedMemoryStream);
            stream.Dispose();
            stream.Close();
            return compressedMemoryStream;
        }
        internal static int Read_Int32(Stream stream)
        {
            byte[] buff = new byte[4];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToInt32(buff, 0);
        }
        internal static ushort Read_UInt16(Stream stream)
        {
            byte[] buff = new byte[2];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToUInt16(buff, 0);
        }
        internal static short Read_Int16(Stream stream)
        {
            byte[] buff = new byte[2];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToInt16(buff, 0);
        }
        internal static byte[] Read_ByteArray(Stream stream, uint length)
        {
            byte[] bytes = new byte[length];
            stream.Read(bytes);
            return bytes;
        }

    }
}
