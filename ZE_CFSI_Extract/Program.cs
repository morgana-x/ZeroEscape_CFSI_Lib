using ZE_CFSI_Lib;
public partial class Program
{
    public static void Main(string[] args)
    {
        string filePath = "";
        if (args.Length ==0)
        {
            Console.WriteLine("Drag and drop file to extract!");
            filePath = Console.ReadLine().Replace("\"", "");
        }
        else
            filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File {filePath} doesn't exist!");
            return;
        }

        Console.WriteLine("Extracting...");
        new CFSI_Lib(filePath).ExtractAll(filePath + "_extracted");
        Console.WriteLine("Extracted!");
    }
}