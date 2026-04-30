using System.Diagnostics;

internal static class AdminActions
{
    public static void OpenConfig(string configPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    UseShellExecute = false,
                    ArgumentList = { configPath }
                });
            }
            else
            {
                Console.WriteLine($"Arquivo de configuracao: {configPath}");
                Console.WriteLine("A abertura automatica do editor esta disponivel no Windows.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nao foi possivel abrir o arquivo de configuracao: {ex.Message}");
            Console.WriteLine(configPath);
        }
    }
}