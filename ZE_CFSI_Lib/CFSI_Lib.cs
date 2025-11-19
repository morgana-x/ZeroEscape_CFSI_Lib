namespace ZE_CFSI_Lib
{
    public class CFSI_Lib
    {
        public Stream stream;
        public List<CFSI_File> Files = new List<CFSI_File>();
        public long DataSectionStart = 0;

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

        public void Dispose()
        {
            stream.Dispose();
            stream.Close();
            Files.Clear();
        }
        public byte[] GetFileData(CFSI_File file)
        {
            long filePosition = DataSectionStart + file.Offset;
            stream.Position = filePosition;

            if (file.Compressed)
            {
                uint uncompressedSize = CFSI_Util.Read_UInt32(stream);

                ushort gzipSignature = CFSI_Util.Read_UInt16(stream);
                if (gzipSignature != 0x8b1f)
                {
                    throw new InvalidDataException("Invalid GZIP signature for compressed file");
                }

                stream.Position -= 2;

                int compressedSize = (int)(file.Size - 4);
                MemoryStream inputStream = new(CFSI_Util.Read_ByteArray(stream, compressedSize));
                MemoryStream tempStream = new();

                ICSharpCode.SharpZipLib.GZip.GZip.Decompress(inputStream, tempStream, false);

                inputStream.Dispose();
                inputStream.Close();

                return tempStream.ToArray();
            }
            else
            {
                return CFSI_Util.Read_ByteArray(stream, (int)file.Size);
            }
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

        private void ReadHeader()
        {
            stream.Seek(0, SeekOrigin.Begin);

            uint numOfFolders = CFSI_Util.Read_CFSI_VINT(stream);

            for (ushort i = 0; i < numOfFolders; i++)
            {
                string folderName = CFSI_Util.Read_CFSI_String(stream);

                if (folderName.Length == 1 && folderName[0] == (char)0) // For Root folder
                    folderName = "\\";

                uint numOfFiles = CFSI_Util.Read_CFSI_VINT(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                {
                    string fileName = CFSI_Util.Read_CFSI_String(stream);
                    uint fileOffset = CFSI_Util.Read_UInt32(stream) * 16;
                    uint fileSize = CFSI_Util.Read_UInt32(stream);
                    Files.Add(new CFSI_File(fileName, folderName + fileName, fileOffset, fileSize));
                }
            }

            DataSectionStart = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            foreach (var file in Files)
            {
                if (file.Size < 6)
                    continue;

                stream.Position = DataSectionStart + file.Offset;

                uint uncompressedSize = CFSI_Util.Read_UInt32(stream);

                ushort gzipSignature = CFSI_Util.Read_UInt16(stream);

                file.Compressed = (gzipSignature == 0x8b1f);
            }
        }

        public static void Repack(string folder, string outPath = "")
        {
            if (outPath == "")
                outPath = folder + ".cfsi";
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string? SourceDirectoryPath = Directory.GetParent(folder)?.FullName + "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            List<string> SubDirectories = new List<string>();
            List<List<string>> SubDirectoriesFiles = new List<List<string>>();
            List<string> FilePaths = Directory.GetFiles(folder, "*", new EnumerationOptions() { RecurseSubdirectories = true }).Select(fn => fn).OrderBy(f => CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath, f)).ToList();
            List<string> FilePaths_Reordered = new List<string>();
            List<long> FileOffsets = new List<long>();

            MemoryStream dataStream = new MemoryStream();

            foreach (var file in FilePaths)
            {
                string folderPath = CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath, file);

                if (!SubDirectories.Contains(folderPath))
                {
                    SubDirectories.Add(folderPath);
                    SubDirectoriesFiles.Add(new List<string>());
                }
                SubDirectoriesFiles[SubDirectories.IndexOf(folderPath)].Add(file);
            }

            FilePaths.Clear();

            CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectories.Count);

            int relative_data_offset = 0;

            for (int i = 0; i < SubDirectories.Count; i++)
            {
                string folderPath = SubDirectories[i].Replace(SourceDirectoryPath, "");
                if (!folderPath.EndsWith("\\") && !folderPath.EndsWith("/"))
                {
                    folderPath += "/";
                }
                CFSI_Util.Write_CFSI_String(cfsiStream, folderPath);
                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectoriesFiles[i].Count);

                foreach (var file in SubDirectoriesFiles[i])
                {
                    FilePaths_Reordered.Add(file);
                    FileOffsets.Add(relative_data_offset);
                    CFSI_Util.Write_CFSI_String(cfsiStream, file.Replace(SourceDirectoryPath, "").Replace(SubDirectories[i] + "\\", ""));
                    cfsiStream.Write(BitConverter.GetBytes(relative_data_offset / 16));
                    int fileSize = (CFSI_Util.CFSI_ShouldBeCompressed(file)) ? CFSI_Util.CFSI_Get_Compressed_Size(file) : CFSI_Util.CFSI_Get_Size(file);
                    cfsiStream.Write(BitConverter.GetBytes(fileSize));
                    relative_data_offset += CFSI_Util.CFSI_Get_Aligned(fileSize);
                }
                relative_data_offset = CFSI_Util.CFSI_Get_Aligned(relative_data_offset);
            }
            long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
            for (int i = 0; i < FilePaths_Reordered.Count; i++)
            {
                cfsiStream.Position = dataSection + FileOffsets[i];

                Stream fs;
                if (CFSI_Util.CFSI_ShouldBeCompressed(FilePaths_Reordered[i]))
                    fs = CFSI_Util.CFSI_Get_Compressed(FilePaths_Reordered[i]);
                else
                    fs = File.OpenRead(FilePaths_Reordered[i]);
                fs.Seek(0, SeekOrigin.Begin);
                fs.CopyTo(cfsiStream);
                fs.Dispose(); fs.Close();
            }

            long fileLength = cfsiStream.Length;
            int remainder = (int)(fileLength % 16);
            if (remainder != 0)
            {
                int paddingNeeded = 16 - remainder;
                byte[] padding = new byte[paddingNeeded];
                cfsiStream.Write(padding, 0, paddingNeeded);
            }

            cfsiStream.Close();
            cfsiStream.Dispose();
        }
    }
}
