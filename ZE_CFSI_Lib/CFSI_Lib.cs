using static ZE_CFSI_Lib.Util;
namespace ZE_CFSI_Lib
{
    public class CFSI_Lib
    {
        public Stream stream;
        public List<CFSI_File> Files = new List<CFSI_File>();

        public CFSI_Lib(Stream newStream)
        {
            this.stream = newStream;
            ReadHeader();
        }
        public CFSI_Lib(string filePath)
        {
            stream = File.OpenRead(filePath);
            ReadHeader();
        }
        public byte[] GetFileData(CFSI_File file)
        {
            if (file.FileOffset == 0) { return new byte[0]; }
            stream.Position = file.FileOffset;
            return ReadBytes(stream, (uint)file.Size);
        }
        public void ExtractFile(CFSI_File file, string OutFolder)
        {
            string outPath = OutFolder + file.Path.Replace("/", "\\");
            Directory.CreateDirectory(OutFolder);
            Directory.CreateDirectory(outPath.Replace(file.Name, ""));
            File.WriteAllBytes(outPath, GetFileData(file));
        }
        public void ExtractAll(string OutFolder)
        {
            if (!OutFolder.EndsWith("\\")) OutFolder += "\\";
            foreach (CFSI_File file in Files)
                ExtractFile(file, OutFolder);
        }
        private void ReadHeader()
        {
            stream.Seek(0, SeekOrigin.Begin);

            uint numOfFolders = ReadCfsiStupidShort(stream);

            for (ushort i = 0; i < numOfFolders; i++)
            {
                string folderName = ReadCfsiString(stream);

                if (folderName.Length == 1 && folderName[0] == (char)0)
                    folderName = "\\";

                uint numOfFiles = ReadCfsiStupidShort(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                {
                    string fileName = ReadCfsiString(stream);
                    int offset = ReadInt(stream);
                    int size = ReadInt(stream);
                    Files.Add(new CFSI_File(fileName, folderName + fileName, offset, size));
                }
            }
            SkipWhiteSpace(stream);
            foreach (var file in Files)
            {
                file.FileOffset = stream.Position;
                stream.Position += file.Size;
                SkipWhiteSpace(stream);
            }
        }
    }
    
}
