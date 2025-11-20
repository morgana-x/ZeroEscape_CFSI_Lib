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
                    stream?.Close();
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
            string outPath = Path.Combine(OutFolder, file.Path);
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
                long folderNameStart = stream.Position;
                string folderName = CFSI_Util.Read_CFSI_String(stream);

                string originalFolderName = folderName;

                if (folderName.Length == 1 && folderName[0] == (char)0)
                    folderName = "\\";

                uint numOfFiles = CFSI_Util.Read_CFSI_VINT(stream);

                for (ushort a = 0; a < numOfFiles; a++)
                {
                    long fileEntryStart = stream.Position;

                    string fileName = CFSI_Util.Read_CFSI_String(stream);
                    uint originalOffsetValue = CFSI_Util.Read_UInt32(stream);
                    uint originalSizeValue = CFSI_Util.Read_UInt32(stream);

                    uint fileOffset = originalOffsetValue * 16;
                    uint fileSize = originalSizeValue;

                    string fullPath = originalFolderName == "\0" ? fileName : originalFolderName + fileName;

                    var file = new CFSI_File(fileName, fullPath, fileOffset, fileSize)
                    {
                        OriginalOffsetValue = originalOffsetValue,
                        OriginalSizeValue = originalSizeValue,
                        OriginalHeaderPosition = fileEntryStart
                    };

                    long currentPos = stream.Position;
                    stream.Position = fileEntryStart;
                    int headerLength = (int)(currentPos - fileEntryStart);
                    file.OriginalHeaderBytes = CFSI_Util.Read_ByteArray(stream, headerLength);
                    stream.Position = currentPos;

                    Files.Add(file);
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

            string originalCfsiPath = folder.TrimEnd('\\') + ".cfsi";
            if (!File.Exists(originalCfsiPath))
            {
                throw new FileNotFoundException($"Original CFSI file not found: {originalCfsiPath}");
            }

            using (var originalLib = new CFSI_Lib(originalCfsiPath))
            {
                FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);

                foreach (var file in originalLib.Files)
                {
                    cfsiStream.Write(file.OriginalHeaderBytes);
                }

                long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
                while (cfsiStream.Position < dataSection)
                {
                    cfsiStream.WriteByte(0);
                }

                foreach (var file in originalLib.Files)
                {
                    cfsiStream.Position = dataSection + file.Offset;

                    string filePath = Path.Combine(folder, file.Path.Replace("/", "\\").Replace("\\", Path.DirectorySeparatorChar.ToString()));
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"File not found: {filePath}");
                    }

                    byte[] fileData;
                    if (file.Compressed)
                    {
                        using var fs = File.OpenRead(filePath);
                        using var compressedStream = CFSI_Util.CFSI_Get_Compressed(fs);
                        fileData = new byte[compressedStream.Length];
                        compressedStream.Position = 0;
                        compressedStream.Read(fileData, 0, fileData.Length);
                    }
                    else
                    {
                        fileData = File.ReadAllBytes(filePath);
                    }

                    cfsiStream.Write(fileData, 0, fileData.Length);

                    long fileEnd = dataSection + file.Offset + fileData.Length;
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

                cfsiStream.Close();
                cfsiStream.Dispose();
            }
        }

        public void RepackWithOriginalStructure(string outPath, string modifiedFolder)
        {
            if (!modifiedFolder.EndsWith("\\"))
                modifiedFolder += "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);

            stream.Seek(0, SeekOrigin.Begin);

            long headerEnd = DataSectionStart;
            byte[] headerData = new byte[headerEnd];
            stream.Read(headerData, 0, (int)headerEnd);

            cfsiStream.Write(headerData, 0, headerData.Length);

            long currentPos = cfsiStream.Position;
            long dataSection = CFSI_Util.CFSI_Get_Aligned(currentPos);

            if (currentPos < dataSection)
            {
                byte[] padding = new byte[dataSection - currentPos];
                cfsiStream.Write(padding, 0, padding.Length);
            }

            foreach (var file in Files)
            {
                cfsiStream.Position = dataSection + file.Offset;

                string filePath = Path.Combine(modifiedFolder, file.Path.Replace("/", "\\").Replace("\\", Path.DirectorySeparatorChar.ToString()));
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                byte[] fileData;
                if (file.Compressed)
                {
                    using var fs = File.OpenRead(filePath);
                    using var compressedStream = CFSI_Util.CFSI_Get_Compressed(fs);
                    fileData = new byte[compressedStream.Length];
                    compressedStream.Position = 0;
                    compressedStream.Read(fileData, 0, fileData.Length);

                    if (fileData.Length > file.Size + 1024)
                    {
                        Console.WriteLine($"Warning: File {file.Name} compressed size ({fileData.Length}) may exceed original size ({file.Size})");
                    }
                }
                else
                {
                    fileData = File.ReadAllBytes(filePath);

                    if (fileData.Length != file.Size)
                    {
                        Console.WriteLine($"Warning: File {file.Name} size mismatch (current: {fileData.Length}, original: {file.Size})");
                    }
                }

                cfsiStream.Write(fileData, 0, fileData.Length);

                long fileEnd = dataSection + file.Offset + fileData.Length;
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

            cfsiStream.Close();
            cfsiStream.Dispose();
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
                else if (structure.FullHeaderBytes != null && structure.FullHeaderBytes.Length > 0)
                {
                    cfsiStream.Write(structure.FullHeaderBytes, 0, structure.FullHeaderBytes.Length);
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
            var filesByFolder = new Dictionary<string, List<CFSI_File>>();

            foreach (var file in files)
            {
                string folderPath = ExtractOriginalFolderPath(file.Path);

                if (!filesByFolder.ContainsKey(folderPath))
                    filesByFolder[folderPath] = new List<CFSI_File>();

                filesByFolder[folderPath].Add(file);
            }

            WriteCorrectVINT(stream, (ushort)filesByFolder.Count);

            foreach (var folderPair in filesByFolder)
            {
                string folderName = folderPair.Key;
                var folderFiles = folderPair.Value;

                WriteOriginalFolderString(stream, folderName);

                WriteCorrectVINT(stream, (ushort)folderFiles.Count);

                foreach (var file in folderFiles)
                {
                    CFSI_Util.Write_CFSI_String(stream, file.Name);

                    byte[] offsetBytes = BitConverter.GetBytes(file.OriginalOffsetValue);
                    stream.Write(offsetBytes, 0, 4);

                    byte[] sizeBytes = BitConverter.GetBytes(file.OriginalSizeValue);
                    stream.Write(sizeBytes, 0, 4);
                }
            }
        }

        private static string ExtractOriginalFolderPath(string filePath)
        {
            int lastSlash = filePath.LastIndexOf('/');
            if (lastSlash == -1) lastSlash = filePath.LastIndexOf('\\');

            if (lastSlash >= 0)
            {
                return filePath.Substring(0, lastSlash + 1);
            }

            return "\0"; 
        }

        private static void WriteCorrectVINT(Stream stream, ushort num)
        {
            if (num < 0xF8)
            {
                stream.WriteByte((byte)num);
            }
            else
            {
                stream.WriteByte(0xFC);
                byte[] bytes = BitConverter.GetBytes(num);
                stream.WriteByte(bytes[0]); 
                stream.WriteByte(bytes[1]); 
            }
        }

        private static void WriteOriginalFolderString(Stream stream, string folderName)
        {
            if (folderName == "\0")
            {
                stream.WriteByte(1);
                stream.WriteByte(0);
            }
            else
            {
                byte[] bytes = Encoding.ASCII.GetBytes(folderName);
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private class StructureData
        {
            public long DataSectionStart { get; set; }
            public List<CFSI_File>? Files { get; set; }
            public byte[]? FullHeaderBytes { get; set; }
            public string? FullHeaderBytesBase64 { get; set; } 
        }
    }
}
