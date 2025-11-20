using ZE_CFSI_Lib;
using System.Text.Json;

public partial class Program
{
    static void Execute(string filePath)
    {
        if (Directory.Exists(filePath))
        {
            Console.WriteLine("Packing...");

            string packJsonPath = Path.ChangeExtension(filePath, ".json");
            if (!File.Exists(packJsonPath))
            {
                Console.WriteLine($"JSON file {packJsonPath} not found!");
                return;
            }

            try
            {
                CFSI_Lib.RepackFromJson(filePath, packJsonPath, filePath + ".cfsi");
                Console.WriteLine("Packed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Packing failed: {ex.Message}");
            }
            return;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File {filePath} does not exist!");
            return;
        }

        Console.WriteLine("Extracting...");

        var cfsi = new CFSI_Lib(filePath);
        string extractPath = Path.Combine(
            Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory(),
            Path.GetFileNameWithoutExtension(filePath)
        );

        cfsi.ExtractAll(extractPath);

        string extractJsonPath = Path.ChangeExtension(filePath, ".json");
        cfsi.ExportStructureToJson(extractJsonPath);

        cfsi.Dispose();
        Console.WriteLine("Extracted successfully!");
    }

    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            Execute(args[0]);
            return;
        }

        while (true)
        {
            Console.WriteLine("Drag and drop a file to extract!");
            Console.WriteLine("Or drag and drop a folder to repack!");
            string? filePath = Console.ReadLine()?.Replace("\"", "");

            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine("Input path is empty, please re-enter!");
                continue;
            }

            Execute(filePath);
        }
    }
}
