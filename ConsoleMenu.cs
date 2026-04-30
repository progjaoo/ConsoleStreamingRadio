internal sealed class ConsoleMenu
{
    private readonly ConfigService _configService;
    private readonly AudioDeviceService _audioDevices;
    private readonly StreamPlayerService _player;

    public ConsoleMenu(ConfigService configService, AudioDeviceService audioDevices, StreamPlayerService player)
    {
        _configService = configService;
        _audioDevices = audioDevices;
        _player = player;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        using var playerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var playerTask = _player.RunAsync(playerCts.Token);

        while (!cancellationToken.IsCancellationRequested)
        {
            PrintHeader(_configService.LoadOrCreate());
            PrintMenu();

            var option = Console.ReadLine()?.Trim();

            switch (option)
            {
                case "1":
                    ShowStatus();
                    Pause();
                    break;

                case "2":
                    ShowDevices();
                    Pause();
                    break;

                case "3":
                    SelectAudioDevice();
                    break;

                case "4":
                    ChangeStreamUrl();
                    break;

                case "5":
                    AdminActions.OpenConfig(_configService.ConfigPath);
                    Pause();
                    break;

                case "6":
                    _player.RequestRestart();
                    Console.WriteLine("Player local reiniciado.");
                    Pause();
                    break;

                case "7":
                    WindowsServiceCommands.Install(_configService.LoadOrCreate());
                    Pause();
                    break;

                case "8":
                    WindowsServiceCommands.Start(_configService.LoadOrCreate());
                    Pause();
                    break;

                case "9":
                    WindowsServiceCommands.Stop(_configService.LoadOrCreate());
                    Pause();
                    break;

                case "10":
                    WindowsServiceCommands.Restart(_configService.LoadOrCreate());
                    Pause();
                    break;

                case "11":
                    WindowsServiceCommands.Uninstall(_configService.LoadOrCreate());
                    Pause();
                    break;

                case "0":
                    playerCts.Cancel();
                    await WaitForPlayerToStop(playerTask);
                    return 0;

                default:
                    Console.WriteLine("Opcao invalida.");
                    Pause();
                    break;
            }
        }

        playerCts.Cancel();
        await WaitForPlayerToStop(playerTask);
        return 0;
    }

    private static void PrintHeader(AppConfig config)
    {
        Console.Clear();
        Console.WriteLine("---------------------------------------------------------------------------------------");
        Console.WriteLine("                                   GTF RX Link                                         ");
        Console.WriteLine("---------------------------------------------------------------------------------------");
        Console.WriteLine($"Software desenvolvido por GRUPO GTF e licenciado para: {config.Console.LicensedTo}");
        Console.WriteLine();
        Console.WriteLine($"Stream atual: {config.Stream.Url}");
        Console.WriteLine($"Placa configurada: {FormatDeviceName(config.Audio)}");
        Console.WriteLine($"Configuracao: {Path.Combine(AppContext.BaseDirectory, ConfigService.DefaultFileName)}");
        Console.WriteLine();
    }

    private static string FormatDeviceName(AudioSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputDeviceName))
        {
            return $"{settings.OutputDeviceName} ({settings.Backend})";
        }

        return $"saida padrao do Windows ({settings.Backend})";
    }

    private static void PrintMenu()
    {
        Console.WriteLine("1  - Mostrar status do player local");
        Console.WriteLine("2  - Listar placas de audio");
        Console.WriteLine("3  - Selecionar placa de audio");
        Console.WriteLine("4  - Alterar URL do stream");
        Console.WriteLine("5  - Abrir arquivo de configuracao");
        Console.WriteLine("6  - Reiniciar player local");
        Console.WriteLine("7  - Instalar servico Windows");
        Console.WriteLine("8  - Iniciar servico Windows");
        Console.WriteLine("9  - Parar servico Windows");
        Console.WriteLine("10 - Reiniciar servico Windows");
        Console.WriteLine("11 - Remover servico Windows");
        Console.WriteLine("0  - Sair");
        Console.WriteLine();
        Console.Write("Opcao: ");
    }

    private void ShowStatus()
    {
        var status = _player.Status;

        Console.WriteLine();
        Console.WriteLine($"Estado: {status.State}");
        Console.WriteLine($"Mensagem: {status.Message}");
        Console.WriteLine($"Stream: {status.StreamUrl ?? "-"}");
        Console.WriteLine($"Audio: {status.AudioDeviceName ?? "-"}");
        Console.WriteLine($"Atualizado em: {status.Timestamp:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Log: {AppLogger.LogPath}");
    }

    private void ShowDevices()
    {
        var devices = _audioDevices.ListOutputDevices();

        Console.WriteLine();

        if (devices.Count == 0)
        {
            Console.WriteLine("Nenhuma placa de audio encontrada.");
            return;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var marker = device.IsDefault ? " [padrao]" : string.Empty;
            Console.WriteLine($"{i + 1}. {device.Name}{marker}");
            Console.WriteLine($"   Backend: {device.Backend}");
            Console.WriteLine($"   Id: {device.Id}");
        }
    }

    private void SelectAudioDevice()
    {
        var devices = _audioDevices.ListOutputDevices();

        Console.WriteLine();
        Console.WriteLine("0. Usar saida padrao do Windows");

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var marker = device.IsDefault ? " [padrao]" : string.Empty;
            Console.WriteLine($"{i + 1}. {device.Name} ({device.Backend}){marker}");
        }

        Console.WriteLine();
        Console.Write("Escolha a placa: ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out var selected))
        {
            Console.WriteLine("Opcao invalida.");
            Pause();
            return;
        }

        var config = _configService.LoadOrCreate();

        if (selected == 0)
        {
            config.Audio.OutputDeviceId = string.Empty;
            config.Audio.OutputDeviceName = string.Empty;
            config.Audio.WaveOutDeviceNumber = -1;
            config.Audio.Backend = "Wasapi";
            _configService.Save(config);
            _player.RequestRestart();
            Console.WriteLine("Saida padrao selecionada.");
            Pause();
            return;
        }

        var selectedDevice = _audioDevices.FindByMenuIndex(selected);

        if (selectedDevice is null)
        {
            Console.WriteLine("Placa nao encontrada.");
            Pause();
            return;
        }

        config.Audio.Backend = selectedDevice.Backend;
        config.Audio.OutputDeviceId = selectedDevice.Id;
        config.Audio.OutputDeviceName = selectedDevice.Name;
        config.Audio.WaveOutDeviceNumber = selectedDevice.WaveOutDeviceNumber;
        _configService.Save(config);
        _player.RequestRestart();

        Console.WriteLine($"Placa selecionada: {selectedDevice.Name}");
        Pause();
    }

    private void ChangeStreamUrl()
    {
        var config = _configService.LoadOrCreate();

        Console.WriteLine();
        Console.WriteLine($"URL atual: {config.Stream.Url}");
        Console.Write("Nova URL: ");
        var newUrl = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(newUrl))
        {
            Console.WriteLine("URL mantida.");
            Pause();
            return;
        }

        config.Stream.Url = newUrl;

        try
        {
            _configService.Save(config);
            _player.RequestRestart();
            Console.WriteLine("URL atualizada e player reiniciado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"URL invalida: {ex.Message}");
        }

        Pause();
    }

    private static async Task WaitForPlayerToStop(Task playerTask)
    {
        try
        {
            await Task.WhenAny(playerTask, Task.Delay(TimeSpan.FromSeconds(5)));
        }
        catch
        {
            // The player logs its own failures.
        }
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("Pressione ENTER para continuar...");
        Console.ReadLine();
    }
}
