﻿using System.IO.Compression;

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

                    int fileOffset = CFSI_Util.Read_Int32(stream);
                    int fileSize = CFSI_Util.Read_Int32(stream);

                    Files.Add(new CFSI_File(fileName, folderName + fileName, fileOffset, fileSize));
                }
            }

            long dataSection = CFSI_Util.CFSI_Get_Aligned(stream.Position);

            foreach (var file in Files)
            {
                file.FileOffset = dataSection + (file.Offset * 16);
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
        private static bool ShouldBeCompressed(string fileName)
        {
            if (fileName.EndsWith(".orb")) return true;
            if (fileName.EndsWith(".uaz")) return true;
            if (fileName.EndsWith(".rtz")) return true;
            return false;
        }
        public static void Repack(string folder, string outPath="")
        {
            if (outPath == "")
                outPath = folder + ".cfsi";
            if (!folder.EndsWith("\\"))
                folder += "\\";

            string badString = Directory.GetParent(folder).FullName + "\\";

            FileStream cfsiStream = new FileStream(outPath, FileMode.Create, FileAccess.Write);
            string[] SubDirectories = Directory.GetDirectories(folder, "*", new EnumerationOptions() { RecurseSubdirectories = true });
            CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)SubDirectories.Length);
            List<long> FileOffsets = new List<long>();
            List<string> FilePaths = new List<string>();
            for (int i = 0; i < SubDirectories.Length; i++)
            {
                CFSI_Util.Write_CFSI_String(cfsiStream, SubDirectories[i].Replace(badString, ""));
                string[] Files = Directory.GetFiles(SubDirectories[i], "*", new EnumerationOptions() { RecurseSubdirectories = true});
                CFSI_Util.Write_CFSI_VINT(cfsiStream, (ushort)Files.Length);
                long offset = 0;
                for (int x=0; x<Files.Length; x++)
                {
                    FileOffsets.Add(offset);
                    FilePaths.Add(Files[i]);
                    CFSI_Util.Write_CFSI_String(cfsiStream, Files[i].Replace(badString, ""));
                    cfsiStream.Write(BitConverter.GetBytes(offset/16));
                    byte[] fileData = File.ReadAllBytes(Files[i]);
                    cfsiStream.Write(BitConverter.GetBytes(fileData.Length));
                    offset += fileData.Length;
                    
                }
            }
            long dataSection = CFSI_Util.CFSI_Get_Aligned(cfsiStream.Position);
            for (int i = 0; i < FileOffsets.Count; i++)
            {
                cfsiStream.Position = dataSection + FileOffsets[i];
                byte[] fileData = new byte[] { };
                if (ShouldBeCompressed(FilePaths[i]))
                {
                    var gstream = new GZipStream(new FileStream(FilePaths[i], FileMode.Open, FileAccess.Read), CompressionMode.Compress);
                    fileData = new byte[gstream.Length];
                    gstream.Read(fileData);
                }
                else
                    fileData = File.ReadAllBytes(FilePaths[i]);
                
                cfsiStream.Write(fileData);
            }
        }
    }
}
