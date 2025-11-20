using System.Text;

namespace ZE_CFSI_Lib
{
    internal class CFSI_Util
    {
        private const int CFSI_Align = 0x10;

        public static string Read_CFSI_String(Stream stream)
        {
            int stringLen = (int)Read_CFSI_VINT(stream);
            return Encoding.ASCII.GetString(Read_ByteArray(stream, stringLen));
        }

        public static void Write_CFSI_String(Stream stream, string text)
        {
            if (text == "\\" || text == "/" || text == "\0")
            {
                Write_CFSI_VINT(stream, 1);
                stream.WriteByte(0);
                return;
            }

            byte[] bytes = Encoding.ASCII.GetBytes(text.Replace("\\", "/"));
            Write_CFSI_VINT(stream, (ushort)bytes.Length);
            stream.Write(bytes);
        }

        public static uint Read_CFSI_VINT(Stream stream)
        {
            byte firstByte = (byte)stream.ReadByte();

            if (firstByte == 0xF8 || firstByte == 0xFC)
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
            if (num < 0xF8)
            {
                stream.WriteByte((byte)num);
            }
            else
            {
                stream.WriteByte(0xFC);
                stream.Write(BitConverter.GetBytes(num));
            }
        }

        internal static long CFSI_Get_Aligned(long offset, int align = CFSI_Align)
        {
            return offset + CFSI_Get_Padding(offset, align);
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
            string extension = Path.GetExtension(fileName).ToLower();
            return CompressedFileExtensions.Contains(extension) || new FileInfo(fileName).Length > 1024;
        }

        internal static Stream CFSI_Get_Compressed(Stream stream)
        {
            MemoryStream compressedMemoryStream = new MemoryStream();

            // Write uncompressed size header
            compressedMemoryStream.Write(BitConverter.GetBytes((uint)stream.Length), 0, 4);

            using (var gzipStream = new System.IO.Compression.GZipStream(compressedMemoryStream,
                System.IO.Compression.CompressionMode.Compress, true))
            {
                stream.CopyTo(gzipStream);
            }

            compressedMemoryStream.Position = 0;
            return compressedMemoryStream;
        }

        internal static Stream CFSI_Get_Compressed(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return CFSI_Get_Compressed(fs);
            }
        }

        internal static int CFSI_Get_Compressed_Size(Stream stream)
        {
            using (var compressedStream = CFSI_Get_Compressed(stream))
            {
                return (int)compressedStream.Length;
            }
        }

        internal static int CFSI_Get_Compressed_Size(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return CFSI_Get_Compressed_Size(fs);
            }
        }

        internal static int CFSI_Get_Size(string path)
        {
            return (int)new FileInfo(path).Length;
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
