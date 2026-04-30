using System.Text.Json;

internal sealed class ConfigService
{
    public const string DefaultFileName = "radio-config.json";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private AppConfig? _lastKnownGood;

    public ConfigService(string? configPath = null)
    {
        ConfigPath = configPath ?? Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    public string ConfigPath { get; }

    public AppConfig LoadOrCreate()
    {
        lock (_sync)
        {
            EnsureDirectory();

            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = AppConfig.CreateDefault();
                SaveCore(defaultConfig);
                _lastKnownGood = defaultConfig.Clone();
                return defaultConfig;
            }

            if (TryReadValidCore(out var config, out var error))
            {
                _lastKnownGood = config.Clone();
                return config;
            }

            AppLogger.Error($"Configuracao invalida em {ConfigPath}: {error}");
            return (_lastKnownGood ?? AppConfig.CreateDefault()).Clone();
        }
    }

    public void Save(AppConfig config)
    {
        lock (_sync)
        {
            Normalize(config);
            Validate(config);
            EnsureDirectory();
            SaveCore(config);
            _lastKnownGood = config.Clone();
        }
    }

    public bool TryLoadValid(out AppConfig? config, out string error)
    {
        lock (_sync)
        {
            EnsureDirectory();
            return TryReadValidCore(out config, out error);
        }
    }

    public IDisposable Watch(Action onValidConfigChanged)
    {
        EnsureDirectory();

        var directory = Path.GetDirectoryName(ConfigPath) ?? AppContext.BaseDirectory;
        var fileName = Path.GetFileName(ConfigPath);
        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime
        };

        var debounce = new DebouncedAction(TimeSpan.FromMilliseconds(700), () =>
        {
            if (TryLoadValid(out _, out var error))
            {
                AppLogger.Info("Arquivo de configuracao alterado. Reiniciando player.");
                onValidConfigChanged();
                return;
            }

            AppLogger.Error($"Arquivo de configuracao alterado, mas esta invalido: {error}");
        });

        FileSystemEventHandler fileHandler = (_, _) => debounce.Signal();
        RenamedEventHandler renamedHandler = (_, _) => debounce.Signal();

        watcher.Changed += fileHandler;
        watcher.Created += fileHandler;
        watcher.Renamed += renamedHandler;
        watcher.EnableRaisingEvents = true;

        return new DelegateDisposable(() =>
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= fileHandler;
            watcher.Created -= fileHandler;
            watcher.Renamed -= renamedHandler;
            watcher.Dispose();
            debounce.Dispose();
        });
    }

    private bool TryReadValidCore(out AppConfig config, out string error)
    {
        config = AppConfig.CreateDefault();
        error = string.Empty;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (loaded is null)
            {
                error = "JSON vazio ou invalido.";
                return false;
            }

            Normalize(loaded);
            Validate(loaded);
            config = loaded;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.Stream ??= new StreamSettings();
        config.Audio ??= new AudioSettings();
        config.Service ??= new ServiceSettings();
        config.Console ??= new ConsoleSettings();

        config.Stream.Url = config.Stream.Url?.Trim() ?? string.Empty;
        config.Stream.ReconnectDelaySeconds = Math.Clamp(config.Stream.ReconnectDelaySeconds, 1, 300);

        config.Audio.Backend = string.IsNullOrWhiteSpace(config.Audio.Backend)
            ? "Wasapi"
            : config.Audio.Backend.Trim();
        config.Audio.OutputDeviceId = config.Audio.OutputDeviceId?.Trim() ?? string.Empty;
        config.Audio.OutputDeviceName = config.Audio.OutputDeviceName?.Trim() ?? string.Empty;

        config.Service.Name = string.IsNullOrWhiteSpace(config.Service.Name)
            ? "GTFRXTlink"
            : config.Service.Name.Trim();
        config.Service.DisplayName = string.IsNullOrWhiteSpace(config.Service.DisplayName)
            ? "GTF RX Tlink"
            : config.Service.DisplayName.Trim();
        config.Service.Description = config.Service.Description?.Trim() ?? string.Empty;

        config.Console.LicensedTo = string.IsNullOrWhiteSpace(config.Console.LicensedTo)
            ? "KBUM 102,7 FM"
            : config.Console.LicensedTo.Trim();
    }

    private static void Validate(AppConfig config)
    {
        if (!Uri.TryCreate(config.Stream.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Stream.Url deve ser uma URL http ou https valida.");
        }

        if (!string.Equals(config.Audio.Backend, "Wasapi", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(config.Audio.Backend, "WaveOut", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Audio.Backend deve ser Wasapi ou WaveOut.");
        }

        if (string.IsNullOrWhiteSpace(config.Service.Name))
        {
            throw new InvalidOperationException("Service.Name nao pode ficar vazio.");
        }
    }

    private void SaveCore(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(ConfigPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
