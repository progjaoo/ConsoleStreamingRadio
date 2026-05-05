using System.Text;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Windows Services may not have a console handle.
        }

        var configService = new ConfigService();
        var audioDevices = new AudioDeviceService();
        var player = new StreamPlayerService(configService, audioDevices);

        if (args.Length == 0 || IsCommand(args[0], "--console"))
        {
            AppLogger.ConsoleEnabled = true;
            return await new ConsoleMenu(configService, audioDevices, player).RunAsync(CancellationToken.None);
        }

        var command = args[0].Trim().ToLowerInvariant();

        switch (command)
        {
            case "--service":
                AppLogger.ConsoleEnabled = false;
                var serviceConfig = configService.LoadOrCreate();
                return new WindowsServiceHost(
                    serviceConfig.Service.Name,
                    token => player.RunAsync(token)).Run();

            case "--run":
                AppLogger.ConsoleEnabled = HasInteractiveConsole();
                return await RunPlayerOnlyAsync(player, AppLogger.ConsoleEnabled);

            case "--list-devices":
                AppLogger.ConsoleEnabled = true;
                ListDevices(audioDevices);
                return 0;

            case "--open-config":
                AppLogger.ConsoleEnabled = true;
                configService.LoadOrCreate();
                AdminActions.OpenConfig(configService.ConfigPath);
                return 0;

            case "--export-logs":
                AppLogger.ConsoleEnabled = true;
                Console.WriteLine($"Arquivo TXT de logs gerado em: {AppLogger.ExportTextLog()}");
                return 0;

            case "--install-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Install(configService.LoadOrCreate());

            case "--uninstall-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Uninstall(configService.LoadOrCreate());

            case "--start-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Start(configService.LoadOrCreate());

            case "--stop-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Stop(configService.LoadOrCreate());

            case "--restart-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Restart(configService.LoadOrCreate());

            case "--status-service":
                AppLogger.ConsoleEnabled = true;
                return WindowsServiceCommands.Status(configService.LoadOrCreate());

            case "--help":
            case "-h":
            case "/?":
                AppLogger.ConsoleEnabled = true;
                PrintHelp();
                return 0;

            default:
                AppLogger.ConsoleEnabled = true;
                Console.WriteLine($"Comando desconhecido: {args[0]}");
                Console.WriteLine();
                PrintHelp();
                return 1;
        }
    }

    private static bool IsCommand(string value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> RunPlayerOnlyAsync(StreamPlayerService player, bool interactiveConsole)
    {
        using var cts = new CancellationTokenSource();
        EventHandler processExitHandler = (_, _) =>
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The process is already shutting down.
            }
        };

        if (interactiveConsole)
        {
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            Console.WriteLine("Executando player. Pressione CTRL+C para sair.");
        }

        AppDomain.CurrentDomain.ProcessExit += processExitHandler;

        try
        {
            await player.RunAsync(cts.Token);
            return 0;
        }
        finally
        {
            AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
        }
    }

    private static bool HasInteractiveConsole()
    {
        if (!Environment.UserInteractive)
        {
            return false;
        }

        try
        {
            _ = Console.CursorLeft;
            return !Console.IsOutputRedirected;
        }
        catch
        {
            return false;
        }
    }

    private static void ListDevices(AudioDeviceService audioDevices)
    {
        var devices = audioDevices.ListOutputDevices();

        if (devices.Count == 0)
        {
            Console.WriteLine("Nenhuma placa de audio foi encontrada.");
            return;
        }

        Console.WriteLine("Placas de audio encontradas:");
        Console.WriteLine();

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var marker = device.IsDefault ? " [padrao]" : string.Empty;
            Console.WriteLine($"{i + 1}. {device.Name}{marker}");
            Console.WriteLine($"   Backend: {device.Backend}");
            Console.WriteLine($"   Id: {device.Id}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GTF RX Tlink - comandos disponiveis");
        Console.WriteLine();
        Console.WriteLine("  --console           Abre o menu administrativo e executa o player local");
        Console.WriteLine("  --run               Executa apenas o player, sem menu (usado pelo WinSW)");
        Console.WriteLine("  --service           Entrada antiga usada pelo Windows Service via sc.exe");
        Console.WriteLine("  --list-devices      Lista placas de audio de saida");
        Console.WriteLine("  --open-config       Abre o arquivo de configuracao");
        Console.WriteLine("  --export-logs       Gera um arquivo TXT com os logs atuais");
        Console.WriteLine("  --install-service   Instala o servico Windows (WinSW se disponivel)");
        Console.WriteLine("  --uninstall-service Remove o servico Windows (WinSW se disponivel)");
        Console.WriteLine("  --start-service     Inicia o servico Windows (WinSW se disponivel)");
        Console.WriteLine("  --stop-service      Para o servico Windows (WinSW se disponivel)");
        Console.WriteLine("  --restart-service   Reinicia o servico Windows (WinSW se disponivel)");
        Console.WriteLine("  --status-service    Mostra o status do servico Windows (WinSW se disponivel)");
    }
}
