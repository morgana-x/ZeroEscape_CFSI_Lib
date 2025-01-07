using ZE_CFSI_Lib;
public partial class Program
{
    public static void Main(string[] args)
    {
        string filePath = "";
        if (args.Length ==0)
        {
            Console.WriteLine("Drag and drop file to extract!");
            Console.WriteLine("OR Drag and drop folder to repack!");
            filePath = Console.ReadLine().Replace("\"", "");
        }
        else
            filePath = args[0];

        if (!File.Exists(filePath))
        {
            if (Directory.Exists(filePath)) // Packing is not supported yet!!!
            {
                Console.WriteLine("Packing...");
                CFSI_Lib.Repack(filePath);
                Console.WriteLine("packed!");
                return;
            }
            Console.WriteLine($"File {filePath} doesn't exist!");
            return;
        }

        Console.WriteLine("Extracting...");
        new CFSI_Lib(filePath).ExtractAll(filePath + "_extracted");
        Console.WriteLine("Extracted!");
    }
}