using NAudio.Wave;

internal sealed class StreamPlayerService
{
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

        using var watcher = _configService.Watch(RequestRestart);

        while (!cancellationToken.IsCancellationRequested)
        {
            var config = _configService.LoadOrCreate();

            if (!config.Stream.AutoPlay)
            {
                SetStatus(StreamPlayerStatus.Stopped("Autoplay desativado na configuracao."));
                await DelayIgnoringCancellation(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            using var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            lock (_sync)
            {
                _currentPlaybackCts = playbackCts;
            }

            try
            {
                await PlayOnceAsync(config, playbackCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                AppLogger.Info("Reinicio do player solicitado.");
            }
            catch (Exception ex)
            {
                SetStatus(StreamPlayerStatus.Error(config.Stream.Url, ex.Message));
                AppLogger.Error(ex, $"Falha no streaming: {ex.Message}");

                if (!config.Stream.ReconnectEnabled)
                {
                    AppLogger.Error("Reconexao automatica desativada. Player parado.");
                    break;
                }

                AppLogger.Info($"Reconectando em {config.Stream.ReconnectDelaySeconds} segundos.");
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

    private async Task PlayOnceAsync(AppConfig config, CancellationToken cancellationToken)
    {
        SetStatus(StreamPlayerStatus.Connecting(config.Stream.Url));
        AppLogger.Info($"Conectando ao stream: {config.Stream.Url}");

        using var reader = new MediaFoundationReader(config.Stream.Url);
        using var output = _audioDevices.CreateOutput(config.Audio);
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

        output.PlaybackStopped += (_, e) => stopped.TrySetResult(e.Exception);
        output.Init(reader);
        output.Play();

        SetStatus(StreamPlayerStatus.Playing(config.Stream.Url, config.Audio.OutputDeviceName));
        AppLogger.Info($"Retransmissao de streaming ativa: {config.Stream.Url}");

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                output.Stop();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Falha ao parar saida de audio: {ex.Message}");
            }

            stopped.TrySetCanceled(cancellationToken);
        });

        var playbackError = await stopped.Task;

        if (playbackError is not null)
        {
            throw playbackError;
        }

        throw new InvalidOperationException("Streaming interrompido.");
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

internal sealed record StreamPlayerStatus(
    bool IsPlaying,
    string State,
    string Message,
    string? StreamUrl,
    string? AudioDeviceName,
    DateTimeOffset Timestamp)
{
    public static StreamPlayerStatus Stopped(string message)
    {
        return new StreamPlayerStatus(false, "Parado", message, null, null, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Connecting(string streamUrl)
    {
        return new StreamPlayerStatus(false, "Conectando", "Conectando ao stream.", streamUrl, null, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Playing(string streamUrl, string? audioDeviceName)
    {
        return new StreamPlayerStatus(true, "Tocando", "Retransmissao ativa.", streamUrl, audioDeviceName, DateTimeOffset.Now);
    }

    public static StreamPlayerStatus Error(string streamUrl, string message)
    {
        return new StreamPlayerStatus(false, "Erro", message, streamUrl, null, DateTimeOffset.Now);
    }
}
