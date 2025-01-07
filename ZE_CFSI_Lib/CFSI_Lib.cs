namespace ZE_CFSI_Lib
{
    public class CFSI_Lib
    {
        public Stream stream;
        public List<CFSI_File> Files = new List<CFSI_File>();

        public CFSI_Lib(Stream newStream)
        {
            stream = newStream;
            ReadHeader();
        }
        public CFSI_Lib(string filePath)
        {
            stream = File.OpenRead(filePath);
            ReadHeader();
        }
        public byte[] GetFileData(CFSI_File file)
        {
            stream.Position = file.FileOffset;
            return CFSI_Util.Read_ByteArray(stream, (uint)file.Size);
        }
        public void ExtractFile(CFSI_File file, string OutFolder)
        {
            string outPath = OutFolder + file.Path.Replace("/", "\\");
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

            uint numOfFolders = CFSI_Util.Read_CFSI_VINT(stream);

            for (ushort i = 0; i < numOfFolders; i++)
            {
                string folderName = CFSI_Util.Read_CFSI_String(stream);

                if (folderName.Length == 1 && folderName[0] == (char)0)
                    folderName = "\\";

                uint numOfFiles = CFSI_Util.Read_CFSI_VINT(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                {
                    string fileName = CFSI_Util.Read_CFSI_String(stream);

                    int fileUnknownData = CFSI_Util.Read_Int32(stream);
                    int fileSize = CFSI_Util.Read_Int32(stream);

                    Files.Add(new CFSI_File(fileName, folderName + fileName, fileUnknownData, fileSize));
                }
            }
            CFSI_Util.Skip_CFSI_Whitespace(stream);

            foreach (var file in Files)
            {
                file.FileOffset = stream.Position;
                stream.Position += file.Size;
                CFSI_Util.Skip_CFSI_Whitespace(stream);
            }
        }
    }
    
}
