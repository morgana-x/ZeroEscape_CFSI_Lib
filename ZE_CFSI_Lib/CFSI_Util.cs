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
            if (stringLen < 0) return "";

            byte[] bytes = Read_ByteArray(stream, stringLen);
            return Encoding.ASCII.GetString(bytes);
        }
        public static void Write_CFSI_String(Stream stream, string text)
        {

            text = text.Replace("\0", "");
            byte[] bytes = Encoding.ASCII.GetBytes(text.Replace("\\", "/"));

            if (bytes.Length > 255)
                throw new ArgumentException("String too long for CFSI format");

            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
        public static uint Read_CFSI_VINT(Stream stream)
        {
            byte firstByte = (byte)stream.ReadByte();

            if (firstByte == 0xf8)
            {
                byte[] bytes = Read_ByteArray(stream, 2);
                return BitConverter.ToUInt16(bytes, 0);
            }
            else if (firstByte == 0xfc)
            {
                byte[] bytes = Read_ByteArray(stream, 2);
                return BitConverter.ToUInt16(bytes, 0);
            }
            else
            {
                return firstByte;
            }
        }

        public static void Write_CFSI_VINT(Stream stream, ushort num)
        {
            if (num < 0xf8)
            {
                stream.WriteByte((byte)num);
            }
            else if (num <= 0xffff)
            {
                stream.WriteByte(0xfc);
                stream.Write(BitConverter.GetBytes(num));
            }
            else
            {
                throw new ArgumentException("Number too large for CFSI_VINT");
            }
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
            else
                folderPath = "\0";

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
            compressedMemoryStream.Write(new byte[4] { 0, 0, 0, 0 });
            ICSharpCode.SharpZipLib.GZip.GZip.Compress(stream, compressedMemoryStream, false, level: 2, bufferSize: 512);
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
        internal static int CFSI_Get_Compressed_Size(Stream stream)
        {
            Stream compressedMemoryStream = CFSI_Get_Compressed(stream);
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
                length = (int)(stream.Length - stream.Position);

            byte[] bytes = new byte[length];
            int bytesRead = 0;

            while (bytesRead < length)
            {
                int read = stream.Read(bytes, bytesRead, length - bytesRead);
                if (read == 0)
                    break;
                bytesRead += read;
            }

            if (bytesRead != length)
            {
                Array.Resize(ref bytes, bytesRead);
            }

            return bytes;
        }

    }
}
