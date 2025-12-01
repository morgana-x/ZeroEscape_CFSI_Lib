namespace ZE_CFSI_Lib
{
    public class CFSI_Lib
    {
        public Stream stream;
        public List<CFSI_File> Files = new List<CFSI_File>();
        public long DataSectionStart = 0;

        public CFSI_Lib(string filePath) : this(File.OpenRead(filePath)){}
        public CFSI_Lib(Stream newStream)
        {
            stream = newStream;

            stream.Seek(0, SeekOrigin.Begin);

            uint numOfFolders = CFSI_Util.Read_CFSI_VINT(stream);

            for (ushort i = 0; i < numOfFolders; i++)
            {
                string folderName = CFSI_Util.Read_CFSI_String(stream);

                if (folderName.Length == 1 && folderName[0] == (char)0) // For Root folder
                    folderName = "\\";

                uint numOfFiles = CFSI_Util.Read_CFSI_VINT(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                    Files.Add(new CFSI_File(stream, folderName));
            }

            DataSectionStart = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            foreach (var file in Files)
            {
                if (file.Size < 7)
                    continue;
                stream.Position = DataSectionStart + file.Offset + 4;
                file.Compressed = CFSI_Util.Read_UInt16(stream) == 0x8b1f && stream.ReadByte() == 0x8;
            }
        }

        public void Dispose()
        {
            stream.Dispose();
            stream.Close();
            Files.Clear();
        }

        public byte[] GetFileData(CFSI_File file)
        {
            stream.Position = DataSectionStart + file.Offset;
            if (file.Compressed)
            {
                stream.Position += 4;
                MemoryStream inputStream = new(CFSI_Util.Read_ByteArray(stream, (int)(file.Size - 4)));
                MemoryStream tempStream = new();
                ICSharpCode.SharpZipLib.GZip.GZip.Decompress(inputStream, tempStream, false);
                inputStream.Dispose();
                inputStream.Close();
                byte[] data = tempStream.ToArray();
                tempStream.Dispose();
                tempStream.Close();
                return data;
            }
            return CFSI_Util.Read_ByteArray(stream, (int)file.Size);
        }

        public void ExtractFile(CFSI_File file, string OutFolder)
        {
            string outPath = Path.Combine(OutFolder, file.Path);  // +file.Path.Replace("/", "\\");
            Directory.CreateDirectory(outPath.Replace(file.Name, ""));
            File.WriteAllBytes(outPath, GetFileData(file));
        }

        public void ExtractAll(string OutFolder)
        {
            if (!OutFolder.EndsWith("\\")) OutFolder += "\\";
            foreach (CFSI_File file in Files)
                ExtractFile(file, OutFolder);
        }


        public static void Repack(string folder, string outPath="")
        {
            if (outPath == "")
                outPath = folder + ".cfsi";
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string SourceDirectoryPath = Directory.GetParent(folder).FullName + "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);

            List<string> SubDirectories =  new List<string>();
            List<List<string>> SubDirectoriesFiles = new List<List<string>>();


            foreach(var file in Directory.GetFiles(folder, "*", new EnumerationOptions() { RecurseSubdirectories = true }).Select(fn => fn).OrderBy(f => CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath, f)))
            {
                string folderPath = CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath,file);

                if (!SubDirectories.Contains(folderPath))
                {
                    SubDirectories.Add(folderPath);
                    SubDirectoriesFiles.Add(new List<string>());
                }
                SubDirectoriesFiles[SubDirectories.IndexOf(folderPath)].Add(file);
            }


            CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectories.Count);

            MemoryStream dataStream = new MemoryStream();

            long relative_data_offset = 0;

            for (int i = 0; i < SubDirectories.Count; i++)
            {
                CFSI_Util.Write_CFSI_String(cfsiStream, SubDirectories[i].Replace(SourceDirectoryPath, "") + "\\");
                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectoriesFiles[i].Count);

                foreach (var file in SubDirectoriesFiles[i])
                {

                    Stream fs = CFSI_Util.CFSI_Pack_OpenFile(file);
                    uint fileSize = (uint)fs.Length;

                    dataStream.Position = relative_data_offset;
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.CopyTo(dataStream);
                    fs.Dispose(); fs.Close();


                    CFSI_Util.Write_CFSI_String(cfsiStream, file.Replace(SourceDirectoryPath, "").Replace(SubDirectories[i] + "\\", ""));
                    cfsiStream.Write(BitConverter.GetBytes((uint)(relative_data_offset/16)));
                    cfsiStream.Write(BitConverter.GetBytes(fileSize));

                    relative_data_offset += CFSI_Util.CFSI_Get_Aligned(fileSize);
                }

                relative_data_offset = CFSI_Util.CFSI_Get_Aligned(relative_data_offset);
            }

            long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
            cfsiStream.Position = dataSection;

            dataStream.Position = 0;
            dataStream.CopyTo(cfsiStream);

            dataStream.Dispose();
            dataStream.Close();

            cfsiStream.Close();
            cfsiStream.Dispose();
        }
    }
}
