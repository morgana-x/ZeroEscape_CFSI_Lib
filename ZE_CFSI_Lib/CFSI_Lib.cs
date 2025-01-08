using System.IO.Compression;

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
            if (file.Compressed)
            {
                GZipStream gzipStream = new GZipStream(new MemoryStream(CFSI_Util.Read_ByteArray(stream, (uint)file.Size)), CompressionMode.Decompress);
                return CFSI_Util.Read_ByteArray(gzipStream, (uint)file.Size);
            }
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

                    int fileOffset = CFSI_Util.Read_Int32(stream) * 16;
                    int fileSize = CFSI_Util.Read_Int32(stream);

                    Files.Add(new CFSI_File(fileName, folderName + fileName, fileOffset, fileSize));
                }
            }

            long dataSection = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            foreach (var file in Files)
            {
                file.FileOffset = dataSection + file.Offset;
                if (file.Size < 6)
                    continue;
                stream.Position = file.FileOffset + 4;
                if (CFSI_Util.Read_UInt16(stream) != 0x8b1f)
                    continue;
                file.FileOffset += 4;
                file.Size -= 4;
                file.Compressed = true;
            }
        }
     

        // TODO: Make Repack work with 00000000.cfsi / Multiple sub directories

 

        public static void Repack(string folder, string outPath="")
        {
            if (outPath == "")
                outPath = folder + ".cfsi";
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string badString = Directory.GetParent(folder).FullName + "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            List<string> SubDirectories =  new List<string>();
            List<List<string>> SubDirectoriesFiles = new List<List<string>>();

            List<long> FileOffsets = new List<long>();

            List<string> FilePaths = Directory.GetFiles(folder, "*", new EnumerationOptions() { RecurseSubdirectories = true }).Select(fn => fn).OrderBy(f => CFSI_Util.CFSI_Get_FolderPath(badString, f)).ToList();

      
            foreach(var file in FilePaths)
            {
                string folderPath = CFSI_Util.CFSI_Get_FolderPath(badString,file);

                if (!SubDirectories.Contains(folderPath))
                {
                    SubDirectories.Add(folderPath);
                    SubDirectoriesFiles.Add(new List<string>());
                }
                SubDirectoriesFiles[SubDirectories.IndexOf(folderPath)].Add(file);
            }
            CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectories.Count);
            for (int i = 0; i < SubDirectories.Count; i++)
            {
                CFSI_Util.Write_CFSI_String(cfsiStream, SubDirectories[i].Replace(badString, "") + "\\");
                string[] Files = SubDirectoriesFiles[i].ToArray();
                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)Files.Length);
                int offset = 0;
                for (int x=0; x<Files.Length; x++)
                {
                    FileOffsets.Add(offset);
                    CFSI_Util.Write_CFSI_String(cfsiStream,Files[x].Replace(badString, "").Replace(SubDirectories[i] + "\\", ""));
                    cfsiStream.Write(BitConverter.GetBytes((int)(offset/16)));
                    byte[] fileData = File.ReadAllBytes(Files[x]);
                    cfsiStream.Write(BitConverter.GetBytes(fileData.Length));
                    offset += (fileData.Length) + CFSI_Util.CFSI_Get_Padding(fileData.Length);
                    
                }
            }
            long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
            for (int i = 0; i < FilePaths.Count; i++)
            {
                cfsiStream.Position = dataSection + FileOffsets[i];

                if (CFSI_Util.CFSI_ShouldBeCompressed(FilePaths[i]))
                {
                    var s = CFSI_Util.CFSI_GetCompressed(new FileStream(FilePaths[i], FileMode.Open, FileAccess.ReadWrite));
                    s.Position = 0;
                    s.CopyTo(cfsiStream);
                    s.Dispose();
                    s.Close();
                    continue;
                }
                cfsiStream.Write(File.ReadAllBytes(FilePaths[i]));
            }
            cfsiStream.Close();
            cfsiStream.Dispose();
        }
    }
}
