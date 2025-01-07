using System.Text;
namespace ZE_CFSI_Lib
{
    internal class Util
    {
        public static string ReadCfsiString(Stream stream)
        {
            int stringLen = stream.ReadByte();
            byte[] stringBuffer = new byte[stringLen];
            stream.Read(stringBuffer, 0, stringLen);

            return Encoding.ASCII.GetString(stringBuffer);
        }
        public static int ReadInt(Stream stream)
        {
            byte[] buff = new byte[4];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToInt32(buff, 0);
        }
        public static ushort ReadUshort(Stream stream)
        {
            byte[] buff = new byte[2];
            stream.Read(buff, 0, buff.Length);
            return BitConverter.ToUInt16(buff, 0);
        }
        public static byte[] ReadBytes(Stream stream, uint length)
        {
            byte[] bytes = new byte[length];
            stream.Read(bytes);
            return bytes;
        }
        public static byte ReadUnsignedByte(Stream stream)
        {
            return (byte)stream.ReadByte();
        }
        public static uint ReadCfsiStupidShort(Stream stream)
        {
            uint value = (uint)ReadUnsignedByte(stream);
            if (value == 252)
                value = (uint)ReadUshort(stream);
            return value;
        }
        public static void SkipWhiteSpace(Stream stream)
        {
            //long ogPos = stream.Position;
            while (stream.ReadByte() == 0) ;
            stream.Position -= 1;
            //Console.WriteLine($"Whitespace: {stream.Position - ogPos} bytes.");
        }
    }
}
