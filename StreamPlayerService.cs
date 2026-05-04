using NAudio.Wave;

internal sealed class StreamPlayerService
{
    private static readonly HttpClient LinkHealthClient = new();

    private readonly ConfigService _configService;
    private readonly AudioDeviceService _audioDevices;
    private readonly object _sync = new();
    private CancellationTokenSource? _currentPlaybackCts;
    private StreamPlayerStatus _status = StreamPlayerStatus.Stopped("Aguardando inicio.");

    public StreamPlayerService(ConfigService configService, AudioDeviceService audioDevices)
    {
        _configService = configService;
        _audioDevices = audioDevices;
    }

    public StreamPlayerStatus Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AppLogger.Info("Player iniciado.");

        var activeLinkIndex = 0;
        using var watcher = _configService.Watch(RequestRestart);

        while (!cancellationToken.IsCancellationRequested)
        {
            var config = _configService.LoadOrCreate();
            var links = GetEnabledLinks(config.Stream);

            if (!config.Stream.AutoPlay)
            {
                SetStatus(StreamPlayerStatus.Stopped("Autoplay desativado na configuracao."));
                await DelayIgnoringCancellation(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            if (links.Count == 0)
            {
                SetStatus(StreamPlayerStatus.Error(null, null, "Nenhum link de transmissao ativo."));
                AppLogger.Error("Nenhum link de transmissao ativo.");
                await DelayIgnoringCancellation(TimeSpan.FromSeconds(config.Stream.ReconnectDelaySeconds), cancellationToken);
                continue;
            }

            if (activeLinkIndex >= links.Count)
            {
                activeLinkIndex = 0;
            }

            var activeLink = links[activeLinkIndex];
            using var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (_sync)
            {
                _currentPlaybackCts = playbackCts;
            }

            try
            {
                await PlayOnceAsync(config, links, activeLinkIndex, playbackCts.Token);
            }
            catch (PrimaryLinkAvailableException)
            {
                activeLinkIndex = 0;
                AppLogger.Info("Link principal voltou a receber dados. Retornando para o principal.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                activeLinkIndex = 0;
                AppLogger.Info("Reinicio do player solicitado.");
            }
            catch (Exception ex)
            {
                SetStatus(StreamPlayerStatus.Error(activeLink.Name, activeLink.Url, ex.Message));
                AppLogger.Error(ex, $"Falha no link {activeLink.Name}: {ex.Message}");

                if (!config.Stream.ReconnectEnabled)
                {
                    AppLogger.Error("Reconexao automatica desativada. Player parado.");
                    break;
                }

                activeLinkIndex = GetNextLinkIndex(activeLinkIndex, links.Count);
                AppLogger.Info($"Proxima tentativa: {links[activeLinkIndex].Name} em {config.Stream.ReconnectDelaySeconds} segundos.");
                await DelayIgnoringCancellation(TimeSpan.FromSeconds(config.Stream.ReconnectDelaySeconds), cancellationToken);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_currentPlaybackCts, playbackCts))
                    {
                        _currentPlaybackCts = null;
                    }
                }
            }
        }

        SetStatus(StreamPlayerStatus.Stopped("Player finalizado."));
        AppLogger.Info("Player finalizado.");
    }

    public void RequestRestart()
    {
        lock (_sync)
        {
            _currentPlaybackCts?.Cancel();
        }
    }

    private async Task PlayOnceAsync(
        AppConfig config,
        IReadOnlyList<ActiveTransmissionLink> links,
        int activeLinkIndex,
        CancellationToken cancellationToken)
    {
        var activeLink = links[activeLinkIndex];

        SetStatus(StreamPlayerStatus.Connecting(activeLink.Name, activeLink.Url));
        AppLogger.Info($"Conectando ao link {activeLink.Name}: {activeLink.Url}");

        using var reader = CreateStreamReader(activeLink.Url);
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var bufferSeconds = Math.Max(3, config.Stream.BufferSeconds);
        var prebufferSeconds = Math.Clamp(config.Stream.PrebufferSeconds, 1, bufferSeconds);
        var underrunFailSeconds = Math.Max(1, config.Stream.BufferUnderrunFailSeconds);
        var bufferedProvider = new BufferedWaveProvider(reader.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(bufferSeconds),
            DiscardOnBufferOverflow = true,
            ReadFully = true
        };

        var producerTask = Task.Run(
            () => FillBuffer(reader, bufferedProvider, readCts.Token),
            CancellationToken.None);

        try
        {
            await WaitForPrebufferAsync(bufferedProvider, producerTask, prebufferSeconds, cancellationToken);

            using var output = _audioDevices.CreateOutput(config.Audio);
            var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

            output.PlaybackStopped += (_, e) => stopped.TrySetResult(e.Exception);
            output.Init(bufferedProvider);
            output.Play();

            SetStatus(StreamPlayerStatus.Playing(
                activeLink.Name,
                activeLink.Url,
                config.Audio.OutputDeviceName,
                bufferedProvider.BufferedDuration));

            AppLogger.Info($"Retransmissao ativa no link {activeLink.Name}: {activeLink.Url}");
            AppLogger.Info($"Formato de audio recebido: {reader.WaveFormat}");
            AppLogger.Info($"Buffer configurado: {bufferSeconds}s. Pre-buffer inicial: {prebufferSeconds}s.");

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                readCts.Cancel();

                try
                {
                    reader.Dispose();
                    output.Stop();
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Falha ao parar saida de audio: {ex.Message}");
                }

                stopped.TrySetCanceled(cancellationToken);
            });

            await MonitorPlaybackAsync(
                config,
                links,
                activeLinkIndex,
                bufferedProvider,
                producerTask,
                stopped,
                underrunFailSeconds,
                cancellationToken);
        }
        finally
        {
            readCts.Cancel();
        }
    }

    private async Task MonitorPlaybackAsync(
        AppConfig config,
        IReadOnlyList<ActiveTransmissionLink> links,
        int activeLinkIndex,
        BufferedWaveProvider bufferedProvider,
        Task<Exception?> producerTask,
        TaskCompletionSource<Exception?> stopped,
        int underrunFailSeconds,
        CancellationToken cancellationToken)
    {
        var activeLink = links[activeLinkIndex];
        var lastBufferedAudioAt = DateTimeOffset.Now;
        var producerCompletionLogged = false;
        var nextPrimaryCheckAt = DateTimeOffset.Now.AddSeconds(config.Stream.PrimaryRetrySeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (stopped.Task.IsCompleted)
            {
                var playbackError = await stopped.Task;

                if (playbackError is not null)
                {
                    throw playbackError;
                }

                throw new InvalidOperationException("Saida de audio parou.");
            }

            var bufferedDuration = bufferedProvider.BufferedDuration;
            SetStatus(StreamPlayerStatus.Playing(
                activeLink.Name,
                activeLink.Url,
                Status.AudioDeviceName,
                bufferedDuration));

            if (bufferedDuration > TimeSpan.FromMilliseconds(500))
            {
                lastBufferedAudioAt = DateTimeOffset.Now;
            }

            if (!producerCompletionLogged && producerTask.IsCompleted)
            {
                producerCompletionLogged = true;
                var producerError = await producerTask;

                AppLogger.Error(producerError is null
                    ? $"Leitura do link {activeLink.Name} foi encerrada. Tocando o buffer restante."
                    : $"Leitura do link {activeLink.Name} falhou. Tocando o buffer restante. Detalhe: {producerError.Message}");
            }

            if (DateTimeOffset.Now - lastBufferedAudioAt > TimeSpan.FromSeconds(underrunFailSeconds))
            {
                var producerError = producerTask.IsCompleted
                    ? await producerTask
                    : null;

                throw producerError ?? new InvalidOperationException("Buffer de audio esgotado.");
            }

            if (activeLinkIndex > 0 && DateTimeOffset.Now >= nextPrimaryCheckAt)
            {
                nextPrimaryCheckAt = DateTimeOffset.Now.AddSeconds(config.Stream.PrimaryRetrySeconds);

                if (await IsLinkReceivingDataAsync(links[0].Url, config.Stream.LinkHealthTimeoutSeconds, cancellationToken))
                {
                    throw new PrimaryLinkAvailableException();
                }

                AppLogger.Info($"Monitor do link principal: ainda indisponivel. Mantendo {activeLink.Name}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static Exception? FillBuffer(WaveStream reader, BufferedWaveProvider bufferedProvider, CancellationToken cancellationToken)
    {
        var readBufferSize = Math.Max(reader.WaveFormat.AverageBytesPerSecond / 4, reader.WaveFormat.BlockAlign * 1024);
        readBufferSize -= readBufferSize % reader.WaveFormat.BlockAlign;

        if (readBufferSize <= 0)
        {
            readBufferSize = 16 * 1024;
        }

        var readBuffer = new byte[readBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = reader.Read(readBuffer, 0, readBuffer.Length);

                if (bytesRead <= 0)
                {
                    return new InvalidOperationException("Link de transmissao encerrou a leitura de audio.");
                }

                while (!cancellationToken.IsCancellationRequested
                    && bufferedProvider.BufferedBytes + bytesRead > bufferedProvider.BufferLength)
                {
                    Thread.Sleep(100);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    bufferedProvider.AddSamples(readBuffer, 0, bytesRead);
                }
            }

            return null;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested
            && ex is ObjectDisposedException or OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task WaitForPrebufferAsync(
        BufferedWaveProvider bufferedProvider,
        Task<Exception?> producerTask,
        int prebufferSeconds,
        CancellationToken cancellationToken)
    {
        var requiredBuffer = TimeSpan.FromSeconds(prebufferSeconds);

        while (bufferedProvider.BufferedDuration < requiredBuffer)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (producerTask.IsCompleted)
            {
                var producerError = await producerTask;

                if (bufferedProvider.BufferedBytes > 0)
                {
                    return;
                }

                throw producerError ?? new InvalidOperationException("Nao foi possivel preencher o buffer inicial.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static WaveStream CreateStreamReader(string streamUrl)
    {
        var settings = new MediaFoundationReader.MediaFoundationReaderSettings
        {
            RequestFloatOutput = false,
            SingleReaderObject = true,
            RepositionInRead = false
        };

        return new MediaFoundationReader(streamUrl, settings);
    }

    private static IReadOnlyList<ActiveTransmissionLink> GetEnabledLinks(StreamSettings settings)
    {
        return settings.Links
            .Select((link, index) => new ActiveTransmissionLink(index, link.Name, link.Url, link.Enabled))
            .Where(link => link.Enabled && !string.IsNullOrWhiteSpace(link.Url))
            .ToList();
    }

    private static int GetNextLinkIndex(int activeLinkIndex, int linkCount)
    {
        if (linkCount <= 1)
        {
            return 0;
        }

        return (activeLinkIndex + 1) % linkCount;
    }

    private static async Task<bool> IsLinkReceivingDataAsync(string url, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            using var response = await LinkHealthClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeoutCts.Token);
            return bytesRead > 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }

    private void SetStatus(StreamPlayerStatus status)
    {
        lock (_sync)
        {
            _status = status;
        }
    }

    private static async Task DelayIgnoringCancellation(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The outer loop handles cancellation.
        }
    }
}

internal sealed record ActiveTransmissionLink(int OriginalIndex, string Name, string Url, bool Enabled);

internal sealed class PrimaryLinkAvailableException : Exception;

internal sealed record StreamPlayerStatus(
    bool IsPlaying,
    string State,
    string Message,
    string? LinkName,
    string? StreamUrl,
    string? AudioDeviceName,
    TimeSpan? BufferDuration,
    DateTimeOffset Timestamp)
{
    public static StreamPlayerStatus Stopped(string message)
    {
        return new StreamPlayerStatus(false, "Parado", message, null, null, null, null, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Connecting(string linkName, string streamUrl)
    {
        return new StreamPlayerStatus(false, "Conectando", "Conectando ao stream.", linkName, streamUrl, null, null, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Playing(string linkName, string streamUrl, string? audioDeviceName, TimeSpan bufferDuration)
    {
        return new StreamPlayerStatus(true, "Tocando", "Retransmissao ativa.", linkName, streamUrl, audioDeviceName, bufferDuration, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Error(string? linkName, string? streamUrl, string message)
    {
        return new StreamPlayerStatus(false, "Erro", message, linkName, streamUrl, null, null, DateTimeOffset.Now);
    }
}
