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

        private const int CFSI_Align = 0x10; // http://aluigi.org/bms/zero_time_dilemma.bms
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
        internal static int CFSI_Get_Aligned(int offset, int align = CFSI_Align)
        {
            return offset + CFSI_Get_Padding(offset, align);
        }
        internal static long CFSI_Get_Aligned(long offset, int align = CFSI_Align)
        {
            return offset + CFSI_Get_Padding(offset, align);
        }
        internal static int CFSI_Get_Padding(int offset, int align = CFSI_Align)
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

       
        public static bool CFSI_ShouldBeCompressed(string fileName)
        {
            if (fileName.EndsWith(".orb")) return true; // For future reference just note that these archives need to be decompressed via GZIP, then processed
            if (fileName.EndsWith(".uaz")) return true;
            if (fileName.EndsWith(".rtz")) return true;
            if (fileName.EndsWith(".bft")) return true;
            if (fileName.EndsWith(".pfx")) return true;
            return false; //Trying to match compression of these causes errors, these archives should have their own tool anyway
        }
        internal static MemoryStream CFSI_Get_Compressed(Stream stream)
        {
            MemoryStream compressedMemoryStream = new MemoryStream();
            
            var gstream = new GZipStream(compressedMemoryStream, CompressionLevel.Optimal);
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(compressedMemoryStream);
            stream.Dispose();
            stream.Close();
          
            return compressedMemoryStream;
        }
        internal static MemoryStream CFSI_Get_Compressed(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var newStream = CFSI_Get_Compressed(fs);
            fs.Dispose();
            fs.Close();
            return newStream;
        }
        internal static int CFSI_Get_Compressed_Size(Stream stream)
        {
            MemoryStream compressedMemoryStream = CFSI_Get_Compressed(stream);
            int length = (int)compressedMemoryStream.Length;
            compressedMemoryStream.Dispose();
            compressedMemoryStream.Close();
            return length;
        }
        internal static int CFSI_Get_Compressed_Size(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            int length = CFSI_Get_Compressed_Size(fs);
            fs.Dispose();
            fs.Close();
            return (int)length;
        }
        internal static int CFSI_Get_Size(string path)
        {
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            int length = (int)fs.Length;
            fs.Dispose();
            fs.Close();
            return (int)length;
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
