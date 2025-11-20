using System.Text;
using System.Text.Json;

namespace ZE_CFSI_Lib
{
    public class CFSI_Lib : IDisposable
    {
        public Stream stream;
        public List<CFSI_File> Files = new List<CFSI_File>();
        public long DataSectionStart = 0;
        private bool _disposed = false;

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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    stream?.Dispose();
                    Files.Clear();
                }
                _disposed = true;
            }
        }

        public byte[] GetFileData(CFSI_File file)
        {
            long filePosition = DataSectionStart + file.Offset;
            stream.Position = filePosition;

            if (file.Compressed)
            {
                byte[] compressedData = CFSI_Util.Read_ByteArray(stream, (int)file.Size);

                if (compressedData.Length >= 2 && compressedData[0] == 0x1F && compressedData[1] == 0x8B)
                {
                    try
                    {
                        using (MemoryStream inputStream = new MemoryStream(compressedData))
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            ICSharpCode.SharpZipLib.GZip.GZip.Decompress(inputStream, outputStream, false);
                            return outputStream.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException($"GZIP decompression failed for file '{file.Name}': {ex.Message}");
                    }
                }
                else if (compressedData.Length >= 4)
                {
                    Exception? gzipEx = null;
                    Exception? zlibEx = null;

                    try
                    {
                        using (MemoryStream inputStream = new MemoryStream(compressedData, 4, compressedData.Length - 4))
                        using (MemoryStream outputStream = new MemoryStream())
                        {
                            ICSharpCode.SharpZipLib.GZip.GZip.Decompress(inputStream, outputStream, false);
                            return outputStream.ToArray();
                        }
                    }
                    catch (Exception ex)
                    {
                        gzipEx = ex;
                        try
                        {
                            using (MemoryStream inputStream = new MemoryStream(compressedData))
                            using (var inflaterStream = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(inputStream))
                            using (MemoryStream outputStream = new MemoryStream())
                            {
                                inflaterStream.CopyTo(outputStream);
                                return outputStream.ToArray();
                            }
                        }
                        catch (Exception ex2)
                        {
                            zlibEx = ex2;
                            try
                            {
                                using (MemoryStream inputStream = new MemoryStream(compressedData, 2, compressedData.Length - 2))
                                using (var inflaterStream = new ICSharpCode.SharpZipLib.Zip.Compression.Streams.InflaterInputStream(inputStream))
                                using (MemoryStream outputStream = new MemoryStream())
                                {
                                    inflaterStream.CopyTo(outputStream);
                                    return outputStream.ToArray();
                                }
                            }
                            catch (Exception ex3)
                            {
                                throw new InvalidDataException($"All decompression methods failed for file '{file.Name}'. " +
                                    $"GZIP: {gzipEx?.Message ?? "Unknown"}, " +
                                    $"Zlib: {zlibEx?.Message ?? "Unknown"}, " +
                                    $"Zlib(skip2): {ex3.Message}");
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException($"Compressed data too small for file '{file.Name}': {compressedData.Length} bytes");
                }
            }
            else
            {
                return CFSI_Util.Read_ByteArray(stream, (int)file.Size);
            }
        }

        public void ExtractFile(CFSI_File file, string OutFolder)
        {
            string outPath = Path.Combine(OutFolder, file.Path);
            string? directoryPath = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
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
                    uint fileOffset = CFSI_Util.Read_UInt32(stream) * 16;
                    uint fileSize = CFSI_Util.Read_UInt32(stream);
                    Files.Add(new CFSI_File(fileName, folderName + fileName, fileOffset, fileSize));
                }
            }

            DataSectionStart = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            foreach (var file in Files)
            {
                if (file.Size < 2)
                    continue;

                stream.Position = DataSectionStart + file.Offset;

                try
                {
                    byte[] header = CFSI_Util.Read_ByteArray(stream, 2);
                    file.Compressed = (header[0] == 0x1F && header[1] == 0x8B);

                    if (!file.Compressed)
                    {
                        file.Compressed = CFSI_Util.CFSI_ShouldBeCompressed(file.Name);
                    }
                }
                catch
                {
                    file.Compressed = false;
                }
            }
        }

        public static void Repack(string folder, string outPath = "")
        {
            if (string.IsNullOrEmpty(folder))
                throw new ArgumentNullException(nameof(folder), "Folder path cannot be null or empty");

            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(Path.GetDirectoryName(folder) ?? "", Path.GetFileName(folder) + ".cfsi");

            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Source folder not found: {folder}");

            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !folder.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }

            string originalCfsiPath = Path.Combine(Path.GetDirectoryName(folder) ?? "",
                                                 Path.GetFileNameWithoutExtension(folder) + ".cfsi");

            if (!File.Exists(originalCfsiPath))
            {
                string altOriginalPath = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".cfsi";
                if (File.Exists(altOriginalPath))
                    originalCfsiPath = altOriginalPath;
                else
                    throw new FileNotFoundException($"Original CFSI file not found. Tried: {originalCfsiPath} and {altOriginalPath}");
            }

            using (var originalLib = new CFSI_Lib(originalCfsiPath))
            {
                string? outputDir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using (FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    originalLib.stream.Seek(0, SeekOrigin.Begin);
                    byte[] headerData = new byte[originalLib.DataSectionStart];
                    int bytesRead = originalLib.stream.Read(headerData, 0, headerData.Length);
                    if (bytesRead != headerData.Length)
                        throw new IOException("Failed to read complete header data from original CFSI file");

                    cfsiStream.Write(headerData, 0, bytesRead);

                    long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
                    while (cfsiStream.Position < dataSection)
                    {
                        cfsiStream.WriteByte(0);
                    }

                    foreach (var file in originalLib.Files)
                    {
                        long filePosition = dataSection + file.Offset;
                        if (filePosition < 0)
                            throw new InvalidDataException($"Invalid file offset for {file.Path}");

                        cfsiStream.Position = filePosition;

                        string safeFilePath = file.Path.Replace('/', Path.DirectorySeparatorChar);
                        string fullFilePath = Path.Combine(folder, safeFilePath);

                        fullFilePath = Path.GetFullPath(fullFilePath);

                        if (!File.Exists(fullFilePath))
                        {
                            throw new FileNotFoundException($"File not found: {fullFilePath}", fullFilePath);
                        }

                        byte[] fileData;
                        if (file.Compressed)
                        {
                            using (var fs = File.OpenRead(fullFilePath))
                            using (var compressedStream = CFSI_Util.CFSI_Get_Compressed(fs))
                            {
                                fileData = new byte[compressedStream.Length];
                                compressedStream.Position = 0;
                                int compressedBytesRead = compressedStream.Read(fileData, 0, fileData.Length);
                                if (compressedBytesRead != fileData.Length)
                                    throw new IOException($"Failed to read complete compressed data from {fullFilePath}");
                            }
                        }
                        else
                        {
                            fileData = File.ReadAllBytes(fullFilePath);
                        }

                        cfsiStream.Write(fileData, 0, fileData.Length);

                        long fileEnd = cfsiStream.Position;
                        long alignedEnd = CFSI_Util.CFSI_Get_Aligned(fileEnd);
                        while (cfsiStream.Position < alignedEnd)
                        {
                            cfsiStream.WriteByte(0);
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
        }

        public void ExportStructureToJson(string jsonPath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true
            };

            stream.Seek(0, SeekOrigin.Begin);
            byte[] fullHeader = CFSI_Util.Read_ByteArray(stream, (int)DataSectionStart);

            var exportData = new
            {
                DataSectionStart = this.DataSectionStart,
                Files = this.Files,
                FullHeaderBytesBase64 = Convert.ToBase64String(fullHeader)
            };

            string json = JsonSerializer.Serialize(exportData, options);
            File.WriteAllText(jsonPath, json);
        }

        public static void RepackFromJson(string folderPath, string jsonPath, string outPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"JSON file not found: {jsonPath}");

            string jsonContent = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { IncludeFields = true };
            var structure = JsonSerializer.Deserialize<StructureData>(jsonContent, options);

            if (structure == null || structure.Files == null)
                throw new InvalidDataException("Invalid JSON structure");

            using (FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                if (!string.IsNullOrEmpty(structure.FullHeaderBytesBase64))
                {
                    byte[] fullHeaderBytes = Convert.FromBase64String(structure.FullHeaderBytesBase64);
                    cfsiStream.Write(fullHeaderBytes, 0, fullHeaderBytes.Length);
                }
                else
                {
                    RebuildHeader(cfsiStream, structure.Files);
                }

                long currentPos = cfsiStream.Position;
                long dataSection = CFSI_Util.CFSI_Get_Aligned(currentPos);

                if (currentPos < dataSection)
                {
                    byte[] padding = new byte[dataSection - currentPos];
                    cfsiStream.Write(padding, 0, padding.Length);
                }

                foreach (var file in structure.Files)
                {
                    cfsiStream.Position = dataSection + file.Offset;
                    string filePath = Path.Combine(folderPath, file.Path.Replace("/", "\\"));

                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"File not found: {filePath}");

                    byte[] fileData = GetFileDataForRepack(filePath, file);
                    cfsiStream.Write(fileData, 0, fileData.Length);

                    long fileEnd = dataSection + file.Offset + fileData.Length;
                    long alignedEnd = CFSI_Util.CFSI_Get_Aligned(fileEnd);
                    while (cfsiStream.Position < alignedEnd)
                        cfsiStream.WriteByte(0);
                }

                long fileLength = cfsiStream.Length;
                int remainder = (int)(fileLength % 16);
                if (remainder != 0)
                {
                    byte[] padding = new byte[16 - remainder];
                    cfsiStream.Write(padding, 0, padding.Length);
                }
            }
        }

        private static byte[] GetFileDataForRepack(string filePath, CFSI_File file)
        {
            if (file.Compressed)
            {
                using var fs = File.OpenRead(filePath);
                using var compressedStream = CFSI_Util.CFSI_Get_Compressed(fs);
                byte[] fileData = new byte[compressedStream.Length];
                compressedStream.Position = 0;
                compressedStream.Read(fileData, 0, fileData.Length);
                return fileData;
            }
            else
            {
                return File.ReadAllBytes(filePath);
            }
        }

        private static void RebuildHeader(Stream stream, List<CFSI_File> files)
        {
            var filesByFolder = files.GroupBy(f => Path.GetDirectoryName(f.Path) ?? "").ToList();

            CFSI_Util.Write_CFSI_VINT(stream, (ushort)filesByFolder.Count);

            foreach (var folderGroup in filesByFolder)
            {
                string folderName = folderGroup.Key;
                if (string.IsNullOrEmpty(folderName))
                    folderName = "\0";

                CFSI_Util.Write_CFSI_String(stream, folderName);
                CFSI_Util.Write_CFSI_VINT(stream, (ushort)folderGroup.Count());

                foreach (var file in folderGroup)
                {
                    CFSI_Util.Write_CFSI_String(stream, file.Name);
                    stream.Write(BitConverter.GetBytes(file.Offset / 16), 0, 4);
                    stream.Write(BitConverter.GetBytes(file.Size), 0, 4);
                }
            }
        }

        private class StructureData
        {
            public long DataSectionStart { get; set; }
            public List<CFSI_File>? Files { get; set; }
            public string? FullHeaderBytesBase64 { get; set; }
        }
    }
}
