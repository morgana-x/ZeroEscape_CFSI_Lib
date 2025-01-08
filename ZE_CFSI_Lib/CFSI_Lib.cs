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

        public void Dispose()
        {
            stream.Dispose();
            stream.Close();
            Files.Clear();
        }
        public byte[] GetFileData(CFSI_File file, bool decompressCompressedFiles = false)
        {
            stream.Position = file.FileOffset;
            if (file.Compressed && decompressCompressedFiles)
            {
                GZipStream gzipStream = new GZipStream(new MemoryStream(CFSI_Util.Read_ByteArray(stream, (uint)file.Size)), CompressionMode.Decompress);
                return CFSI_Util.Read_ByteArray(gzipStream, (uint)file.Size);
            }
            return CFSI_Util.Read_ByteArray(stream, (uint)file.Size);
        }
        public void ExtractFile(CFSI_File file, string OutFolder, bool decompressCompressedFiles = false)
        {
            string outPath = OutFolder + file.Path.Replace("/", "\\");
            Directory.CreateDirectory(outPath.Replace(file.Name, ""));
            File.WriteAllBytes(outPath, GetFileData(file, decompressCompressedFiles));
        }
        public void ExtractAll(string OutFolder, bool decompressCompressedFiles = false)
        {
            if (!OutFolder.EndsWith("\\")) OutFolder += "\\";
            foreach (CFSI_File file in Files)
                ExtractFile(file, OutFolder, decompressCompressedFiles);
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
        // + Milestone: 0000000.cfsi is repackable now, and the repacked file is re-extractable HOWEVER
        // + Issue: Repacked file is consistently smaller
        // + Issue: Game crashes when loading repacked 0000000.cfsi, unlike other repacked cfsis
        public static void Repack(string folder, string outPath="", bool recompressRequiredFiles = false)
        {
            if (outPath == "")
                outPath = folder + ".cfsi";
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string SourceDirectoryPath = Directory.GetParent(folder).FullName + "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            List<string> SubDirectories =  new List<string>();
            List<List<string>> SubDirectoriesFiles = new List<List<string>>();
            List<string> FilePaths = Directory.GetFiles(folder, "*", new EnumerationOptions() { RecurseSubdirectories = true }).Select(fn => fn).OrderBy(f => CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath, f)).ToList();
            List<string> FilePaths_Reordered = new List<string>();
            List<long> FileOffsets = new List<long>();

            MemoryStream dataStream = new MemoryStream();

            foreach(var file in FilePaths)
            {
                string folderPath = CFSI_Util.CFSI_Get_FolderPath(SourceDirectoryPath,file);

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
                CFSI_Util.Write_CFSI_String(cfsiStream, SubDirectories[i].Replace(SourceDirectoryPath, "") + "\\");
                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectoriesFiles[i].Count);
                SubDirectoriesFiles[i] = SubDirectoriesFiles[i].Select(fn => fn).OrderBy(f => f.Replace(SourceDirectoryPath, "").Replace(SubDirectories[i] + "\\", "")).ToList();
                foreach (var file in SubDirectoriesFiles[i])
                {
                    FilePaths_Reordered.Add(file);

                    FileOffsets.Add(relative_data_offset);

                    CFSI_Util.Write_CFSI_String(cfsiStream, file.Replace(SourceDirectoryPath, "").Replace(SubDirectories[i] + "\\", ""));

                    cfsiStream.Write(BitConverter.GetBytes(relative_data_offset/16)); // Division may not be accurate or done the same as in Zero Time Dillema's tools or something?

                    int fileSize = (recompressRequiredFiles && CFSI_Util.CFSI_ShouldBeCompressed(file)) ? CFSI_Util.CFSI_Get_Compressed_Size(file) : CFSI_Util.CFSI_Get_Size(file);
                    cfsiStream.Write(BitConverter.GetBytes(fileSize));

                    int totalSize = fileSize+CFSI_Util.CFSI_Get_Padding(fileSize);
                    relative_data_offset += totalSize;
                }
                // Some kind of padding added between subdirectories in data???
                // Maybe has different Align to file padding to be annoying
                // For some reason repacked archive is always a bit smaller
                // Repacked 00000000.cfsi crashes game when used, unlike bgm.cfsi and voice.cfsi
                //offset += 20; // Remove if causing issues
                //offset = CFSI_Util.CFSI_Get_Aligned(offset); // Remove if causing issues
            }
            long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
            for (int i = 0; i < FilePaths_Reordered.Count; i++)
            {
                cfsiStream.Position = dataSection + FileOffsets[i];

                if (recompressRequiredFiles && CFSI_Util.CFSI_ShouldBeCompressed(FilePaths_Reordered[i]))
                {
                    var s = CFSI_Util.CFSI_Get_Compressed(FilePaths_Reordered[i]);
                    s.Seek(0, SeekOrigin.Begin);
                    s.CopyTo(cfsiStream);
                    s.Dispose();s.Close();
                    continue;
                }
                cfsiStream.Write(File.ReadAllBytes(FilePaths_Reordered[i]));
            }
            cfsiStream.Close();
            cfsiStream.Dispose();
        }
    }
}
