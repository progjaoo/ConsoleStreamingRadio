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

                case "12":
                    ConfigureTransmissionLinks();
                    break;

                case "13":
                    ExportLogs();
                    break;

                case "14":
                    ConfigureBuffer();
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
        Console.WriteLine($"Links ativos: {config.Stream.Links.Count(link => link.Enabled && !string.IsNullOrWhiteSpace(link.Url))}/3");
        Console.WriteLine($"Buffer: {config.Stream.BufferSeconds}s (pre-buffer: {config.Stream.PrebufferSeconds}s)");
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
        Console.WriteLine("12 - Configurar links de transmissao");
        Console.WriteLine("13 - Exportar logs para TXT");
        Console.WriteLine("14 - Configurar buffer de audio");
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
        Console.WriteLine($"Link: {status.LinkName ?? "-"}");
        Console.WriteLine($"Stream: {status.StreamUrl ?? "-"}");
        Console.WriteLine($"Audio: {status.AudioDeviceName ?? "-"}");
        Console.WriteLine($"Buffer: {FormatBuffer(status.BufferDuration)}");
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
            config.Audio.Backend = "DirectSound";
            _configService.Save(config);
            _player.RequestRestart();
            AppLogger.Info("Saida de audio alterada para padrao do Windows.");
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
        AppLogger.Info($"Saida de audio alterada para {selectedDevice.Name} ({selectedDevice.Backend}).");

        Console.WriteLine($"Placa selecionada: {selectedDevice.Name}");
        Pause();
    }

    private void ChangeStreamUrl()
    {
        var config = _configService.LoadOrCreate();

        Console.WriteLine();
        Console.WriteLine($"URL principal atual: {config.Stream.Url}");
        Console.Write("Nova URL principal: ");
        var newUrl = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(newUrl))
        {
            Console.WriteLine("URL mantida.");
            Pause();
            return;
        }

        config.Stream.Url = newUrl;

        while (config.Stream.Links.Count < 3)
        {
            config.Stream.Links.Add(new TransmissionLinkSettings());
        }

        config.Stream.Links[0].Name = "Principal";
        config.Stream.Links[0].Url = newUrl;
        config.Stream.Links[0].Enabled = true;

        try
        {
            _configService.Save(config);
            _player.RequestRestart();
            AppLogger.Info($"URL principal alterada para {newUrl}.");
            Console.WriteLine("URL principal atualizada e player reiniciado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"URL invalida: {ex.Message}");
        }

        Pause();
    }

    private void ConfigureTransmissionLinks()
    {
        var config = _configService.LoadOrCreate();

        Console.WriteLine();
        Console.WriteLine("Links de transmissao:");
        PrintTransmissionLinks(config);
        Console.WriteLine();
        Console.Write("Escolha o link para alterar (1-3) ou 0 para voltar: ");
        var input = Console.ReadLine();

        if (!int.TryParse(input, out var selected) || selected < 0 || selected > 3)
        {
            Console.WriteLine("Opcao invalida.");
            Pause();
            return;
        }

        if (selected == 0)
        {
            return;
        }

        while (config.Stream.Links.Count < 3)
        {
            config.Stream.Links.Add(new TransmissionLinkSettings());
        }

        var index = selected - 1;
        var link = config.Stream.Links[index];

        Console.WriteLine();
        Console.WriteLine($"Link selecionado: {(index == 0 ? "Principal" : $"Reserva {index}")}");
        Console.WriteLine($"URL atual: {(string.IsNullOrWhiteSpace(link.Url) ? "-" : link.Url)}");
        Console.Write("Nova URL (vazio desativa reservas): ");
        var newUrl = Console.ReadLine()?.Trim() ?? string.Empty;

        if (index == 0 && string.IsNullOrWhiteSpace(newUrl))
        {
            Console.WriteLine("O link principal nao pode ficar vazio.");
            Pause();
            return;
        }

        link.Name = index == 0 ? "Principal" : $"Reserva {index}";
        link.Url = newUrl;
        link.Enabled = index == 0 || !string.IsNullOrWhiteSpace(newUrl);

        if (index == 0)
        {
            config.Stream.Url = newUrl;
        }

        try
        {
            _configService.Save(config);
            _player.RequestRestart();
            AppLogger.Info("Links de transmissao atualizados pelo console.");
            Console.WriteLine("Links atualizados e player reiniciado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Configuracao invalida: {ex.Message}");
        }

        Pause();
    }

    private static void PrintTransmissionLinks(AppConfig config)
    {
        for (var i = 0; i < config.Stream.Links.Count && i < 3; i++)
        {
            var link = config.Stream.Links[i];
            var state = link.Enabled ? "ativo" : "desativado";
            var url = string.IsNullOrWhiteSpace(link.Url) ? "-" : link.Url;
            Console.WriteLine($"{i + 1}. {link.Name} [{state}] - {url}");
        }
    }

    private static void ExportLogs()
    {
        var exportPath = AppLogger.ExportTextLog();
        AppLogger.Info($"Logs exportados para TXT: {exportPath}");
        Console.WriteLine();
        Console.WriteLine($"Arquivo TXT de logs gerado em: {exportPath}");
        Pause();
    }

    private void ConfigureBuffer()
    {
        var config = _configService.LoadOrCreate();

        Console.WriteLine();
        Console.WriteLine($"Buffer total atual: {config.Stream.BufferSeconds}s");
        Console.WriteLine($"Pre-buffer atual: {config.Stream.PrebufferSeconds}s");
        Console.Write("Novo buffer total em segundos (3-120): ");
        var bufferInput = Console.ReadLine();
        Console.Write("Novo pre-buffer em segundos (1 ate buffer total): ");
        var prebufferInput = Console.ReadLine();

        if (!int.TryParse(bufferInput, out var bufferSeconds)
            || !int.TryParse(prebufferInput, out var prebufferSeconds))
        {
            Console.WriteLine("Valores invalidos.");
            Pause();
            return;
        }

        config.Stream.BufferSeconds = bufferSeconds;
        config.Stream.PrebufferSeconds = prebufferSeconds;

        try
        {
            _configService.Save(config);
            _player.RequestRestart();
            AppLogger.Info($"Buffer de audio alterado para {config.Stream.BufferSeconds}s com pre-buffer de {config.Stream.PrebufferSeconds}s.");
            Console.WriteLine("Buffer atualizado e player reiniciado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Configuracao invalida: {ex.Message}");
        }

        Pause();
    }

    private static string FormatBuffer(TimeSpan? bufferDuration)
    {
        return bufferDuration is null
            ? "-"
            : $"{bufferDuration.Value.TotalSeconds:0.0}s";
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
