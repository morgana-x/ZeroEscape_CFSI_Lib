using System.Text.Json;

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
            stream?.Dispose();
            stream?.Close();
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
                byte[] compressedData = CFSI_Util.Read_ByteArray(stream, compressedSize);

                using (var compressedStream = new MemoryStream(compressedData))
                using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
                using (var outputStream = new MemoryStream())
                {
                    gzipStream.CopyTo(outputStream);
                    return outputStream.ToArray();
                }
            }
            else
            {
                return CFSI_Util.Read_ByteArray(stream, (int)file.Size);
            }
        }

        public void ExtractFile(CFSI_File file, string outFolder, List<string>? extractedFiles = null)
        {
            string fullPath = Path.Combine(outFolder, file.Path.Replace("/", "\\"));
            string directory = Path.GetDirectoryName(fullPath) ?? outFolder;

            Directory.CreateDirectory(directory);

            string actualPath = GetUniqueFilePath(fullPath, extractedFiles);

            File.WriteAllBytes(actualPath, GetFileData(file));

            extractedFiles?.Add(actualPath);
        }

        public void ExtractAll(string outFolder)
        {
            if (!Directory.Exists(outFolder))
            {
                Directory.CreateDirectory(outFolder);
            }

            List<string> extractedFiles = new List<string>();

            foreach (CFSI_File file in Files)
            {
                ExtractFile(file, outFolder, extractedFiles);
            }

            GenerateStructureJson(outFolder);
        }

        private void GenerateStructureJson(string outFolder)
        {
            try
            {
                var structureInfo = new
                {
                    SourceCfsiFile = Path.GetFileName(stream is FileStream fs ? fs.Name : "unknown"),
                    ExtractionDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    TotalEntries = Files.Count,
                    ExtractedEntries = Files.Count,
                    Entries = Files.Select(f => new
                    {
                        FullPath = f.Path.Replace("\\", "/"),
                        Offset = f.Offset,
                        Size = f.Size,
                        Extracted = true,
                        OutputPath = Path.Combine(outFolder, f.Path.Replace("/", "\\"))
                    }).ToArray()
                };

                string jsonFilePath = Path.Combine(Path.GetDirectoryName(outFolder) ?? outFolder,
                    Path.GetFileName(outFolder) + ".json");

                string json = JsonSerializer.Serialize(structureInfo, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(jsonFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating JSON structure: {ex.Message}");
            }
        }

        private string GetUniqueFilePath(string filePath, List<string>? extractedFiles = null)
        {
            bool existsOnDisk = File.Exists(filePath);
            bool existsInList = extractedFiles?.Contains(filePath) == true;

            if (!existsOnDisk && !existsInList)
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;

            do
            {
                newFilePath = Path.Combine(directory, $"{fileName}_{counter}{extension}");
                counter++;
            } while (File.Exists(newFilePath) || (extractedFiles?.Contains(newFilePath) == true));

            return newFilePath;
        }

        private void ReadHeader()
        {
            stream.Seek(0, SeekOrigin.Begin);

            uint numOfFolders = CFSI_Util.Read_CFSI_VINT(stream);

            for (ushort i = 0; i < numOfFolders; i++)
            {
                string folderName = CFSI_Util.Read_CFSI_String(stream);

                if (folderName.Length == 1 && folderName[0] == (char)0)
                    folderName = "";

                uint numOfFiles = CFSI_Util.Read_CFSI_VINT(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                {
                    string fileName = CFSI_Util.Read_CFSI_String(stream);
                    uint fileOffset = CFSI_Util.Read_UInt32(stream) * 16;
                    uint fileSize = CFSI_Util.Read_UInt32(stream);

                    string fullPath = string.IsNullOrEmpty(folderName) ? fileName : $"{folderName}/{fileName}";
                    Files.Add(new CFSI_File(fileName, fullPath, fileOffset, fileSize));
                }
            }

            DataSectionStart = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            // Detect compression
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
            if (string.IsNullOrEmpty(outPath))
                outPath = folder + ".cfsi";

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Folder not found: {folder}");

            // Try to use JSON structure file first for better compatibility
            string jsonFilePath = Path.Combine(Path.GetDirectoryName(folder) ?? folder,
                Path.GetFileName(folder) + ".json");

            if (File.Exists(jsonFilePath))
            {
                RepackFromJson(folder, jsonFilePath, outPath);
            }
            else
            {
                RepackFromFolder(folder, outPath);
            }
        }

        public static void RepackFromJson(string folder, string jsonFilePath, string outPath)
        {
            try
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                var structureInfo = JsonSerializer.Deserialize<CfsiStructureInfo>(jsonContent);

                if (structureInfo?.Entries == null)
                    throw new InvalidDataException("Invalid JSON structure file");

                string extractedDir = folder;

                using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs))
                {
                    var folderGroups = structureInfo.Entries
                        .GroupBy(e => Path.GetDirectoryName(e.FullPath)?.Replace("\\", "/") ?? "")
                        .ToList();

                    WriteNum(writer, folderGroups.Count);

                    foreach (var folderGroup in folderGroups)
                    {
                        string folderPath = folderGroup.Key;
                        if (string.IsNullOrEmpty(folderPath))
                        {
                            folderPath = "";
                        }
                        else if (!folderPath.EndsWith("/"))
                        {
                            folderPath += "/";
                        }

                        WriteString(writer, folderPath);
                        WriteNum(writer, folderGroup.Count());

                        foreach (var entry in folderGroup)
                        {
                            string fileName = Path.GetFileName(entry.FullPath);
                            WriteString(writer, fileName);

                            uint offsetValue = entry.Offset / 0x10;
                            writer.Write(offsetValue);
                            writer.Write(entry.Size);
                        }
                    }

                    long currentPos = fs.Position;
                    long alignedPos = (currentPos + 0x0F) & ~0x0F;
                    while (fs.Position < alignedPos)
                    {
                        writer.Write((byte)0);
                    }

                    long dataSectionStart = fs.Position;

                    foreach (var entry in structureInfo.Entries.OrderBy(e => e.Offset))
                    {
                        string filePath = string.IsNullOrEmpty(entry.OutputPath)
                            ? Path.Combine(extractedDir, entry.FullPath.Replace("/", "\\"))
                            : entry.OutputPath;

                        if (!File.Exists(filePath))
                        {
                            Console.WriteLine($"Warning: File not found: {filePath}");
                            continue;
                        }

                        long filePos = dataSectionStart + entry.Offset;
                        if (fs.Position != filePos)
                        {
                            fs.Position = filePos;
                        }

                        byte[] fileData = File.ReadAllBytes(filePath);

                        if (entry.Size != (uint)fileData.Length)
                        {
                            fileData = CompressFileData(fileData);
                        }

                        writer.Write(fileData);

                        long fileEnd = fs.Position;
                        long alignedEnd = (fileEnd + 0x0F) & ~0x0F;
                        while (fs.Position < alignedEnd)
                        {
                            writer.Write((byte)0);
                        }
                    }

                    long finalPos = fs.Position;
                    long alignedFinal = (finalPos + 0x0F) & ~0x0F;
                    while (fs.Position < alignedFinal)
                    {
                        writer.Write((byte)0);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error repacking from JSON: {ex.Message}");
                throw;
            }
        }

        private static void RepackFromFolder(string folder, string outPath)
        {
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string sourceDirectoryPath = Directory.GetParent(folder)?.FullName + "\\";

            using (var cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                List<string> subDirectories = new List<string>();
                List<List<string>> subDirectoriesFiles = new List<List<string>>();

                var filePaths = Directory.GetFiles(folder, "*", new EnumerationOptions()
                { RecurseSubdirectories = true })
                    .Select(fn => fn)
                    .OrderBy(f => CFSI_Util.CFSI_Get_FolderPath(sourceDirectoryPath, f))
                    .ToList();

                List<string> filePathsReordered = new List<string>();
                List<long> fileOffsets = new List<long>();

                foreach (var file in filePaths)
                {
                    string folderPath = CFSI_Util.CFSI_Get_FolderPath(sourceDirectoryPath, file);

                    if (!subDirectories.Contains(folderPath))
                    {
                        subDirectories.Add(folderPath);
                        subDirectoriesFiles.Add(new List<string>());
                    }
                    subDirectoriesFiles[subDirectories.IndexOf(folderPath)].Add(file);
                }

                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)subDirectories.Count);

                long relativeDataOffset = 0; 

                for (int i = 0; i < subDirectories.Count; i++)
                {
                    string folderPath = subDirectories[i].Replace(sourceDirectoryPath, "");
                    if (!folderPath.EndsWith("\\") && !folderPath.EndsWith("/"))
                    {
                        folderPath += "/";
                    }

                    CFSI_Util.Write_CFSI_String(cfsiStream, folderPath);
                    CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)subDirectoriesFiles[i].Count);

                    foreach (var file in subDirectoriesFiles[i])
                    {
                        filePathsReordered.Add(file);
                        fileOffsets.Add(relativeDataOffset);

                        CFSI_Util.Write_CFSI_String(cfsiStream,
                            file.Replace(sourceDirectoryPath, "").Replace(subDirectories[i] + "\\", ""));

                        cfsiStream.Write(BitConverter.GetBytes((uint)(relativeDataOffset / 16)));

                        int fileSize = CFSI_Util.CFSI_ShouldBeCompressed(file)
                            ? CFSI_Util.CFSI_Get_Compressed_Size(file)
                            : CFSI_Util.CFSI_Get_Size(file);

                        cfsiStream.Write(BitConverter.GetBytes((uint)fileSize));
                        relativeDataOffset += CFSI_Util.CFSI_Get_Aligned(fileSize);
                    }
                    relativeDataOffset = CFSI_Util.CFSI_Get_Aligned(relativeDataOffset);
                }

                long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);

                for (int i = 0; i < filePathsReordered.Count; i++)
                {
                    cfsiStream.Position = dataSection + fileOffsets[i];

                    using (Stream fs = CFSI_Util.CFSI_ShouldBeCompressed(filePathsReordered[i])
                        ? CFSI_Util.CFSI_Get_Compressed(filePathsReordered[i])
                        : File.OpenRead(filePathsReordered[i]))
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.CopyTo(cfsiStream);
                    }
                }

                long fileLength = cfsiStream.Length;
                int remainder = (int)(fileLength % 16);
                if (remainder != 0)
                {
                    int paddingNeeded = 16 - remainder;
                    byte[] padding = new byte[paddingNeeded];
                    cfsiStream.Write(padding, 0, paddingNeeded);
                }
            }
        }
        public void ExportStructureToJson(string outFolder)
        {
            GenerateStructureJson(outFolder);
        }
        private static byte[] CompressFileData(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            {
                compressedStream.Write(BitConverter.GetBytes((uint)data.Length), 0, 4);

                using (var gzipStream = new System.IO.Compression.GZipStream(compressedStream,
                    System.IO.Compression.CompressionMode.Compress, true))
                {
                    gzipStream.Write(data, 0, data.Length);
                }

                return compressedStream.ToArray();
            }
        }

        private static void WriteNum(BinaryWriter writer, int value)
        {
            if (value < 0xF8)
            {
                writer.Write((byte)value);
            }
            else
            {
                writer.Write((byte)0xFC);
                writer.Write((ushort)value);
            }
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
            if (bytes.Length < 0xF8)
            {
                writer.Write((byte)bytes.Length);
            }
            else
            {
                writer.Write((byte)0xFC);
                writer.Write((ushort)bytes.Length);
            }
            writer.Write(bytes);
        }
    }

    internal class CfsiStructureInfo
    {
        public string? SourceCfsiFile { get; set; }
        public string? ExtractionDate { get; set; }
        public int TotalEntries { get; set; }
        public int ExtractedEntries { get; set; }
        public CfsiEntryInfo[]? Entries { get; set; }
    }

    internal class CfsiEntryInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public bool Extracted { get; set; }
        public string OutputPath { get; set; } = string.Empty;
    }
}
