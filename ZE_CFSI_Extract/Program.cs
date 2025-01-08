using ZE_CFSI_Lib;
public partial class Program
{
    static void Execute(string filePath)
    {
        if (!File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                Console.WriteLine("Packing...");
                CFSI_Lib.Repack(filePath);
                Console.WriteLine("Packed!");
                return;
            }
            Console.WriteLine($"File {filePath} doesn't exist!");
            return;
        }

        Console.WriteLine("Extracting...");
        new CFSI_Lib(filePath).ExtractAll(filePath.Replace(".cfsi", "") + "_extracted");
        Console.WriteLine("Extracted!");
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
            Console.WriteLine("Drag and drop file to extract!");
            Console.WriteLine("OR Drag and drop folder to repack!");
            Execute(Console.ReadLine().Replace("\"", ""));
        }

    }
}