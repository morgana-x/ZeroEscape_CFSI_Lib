using System.Text;
namespace ZE_CFSI_Lib
{
    internal class CFSI_Util
    {
        public static string Read_CFSI_String(Stream stream)
        {
            int stringLen = stream.ReadByte();
            byte[] stringBuffer = new byte[stringLen];
            stream.Read(stringBuffer, 0, stringLen);

            return Encoding.ASCII.GetString(stringBuffer);
        }
        public static int Read_Int32(Stream stream)
        {
            byte[] buff = new byte[4];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToInt32(buff, 0);
        }
        public static ushort Read_UInt16(Stream stream)
        {
            byte[] buff = new byte[2];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToUInt16(buff, 0);
        }
        public static byte[] Read_ByteArray(Stream stream, uint length)
        {
            byte[] bytes = new byte[length];
            stream.Read(bytes);
            return bytes;
        }
        public static byte Read_Byte(Stream stream)
        {
            return (byte)stream.ReadByte();
        }
        public static uint Read_CFSI_VINT(Stream stream)
        {
            uint value = (uint)Read_Byte(stream);
            if (value == 252)
                value = (uint)Read_UInt16(stream);
            return value;
        }
        public static void Skip_CFSI_Whitespace(Stream stream)
        {
            while (stream.ReadByte() == 0) ;
            stream.Position -= 1;
        }
    }
}
