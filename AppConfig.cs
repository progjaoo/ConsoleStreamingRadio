using System.Text.Json;

internal sealed class AppConfig
{
    public StreamSettings Stream { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public ServiceSettings Service { get; set; } = new();
    public ConsoleSettings Console { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Stream = new StreamSettings
            {
                Url = "https://stm19.srvstm.com:7080/stream",
                AutoPlay = true,
                ReconnectEnabled = true,
                ReconnectDelaySeconds = 5,
                BufferSeconds = 20,
                PrebufferSeconds = 5,
                BufferUnderrunFailSeconds = 3,
                PrimaryRetrySeconds = 30,
                LinkHealthTimeoutSeconds = 5,
                Links =
                [
                    new TransmissionLinkSettings
                    {
                        Name = "Principal",
                        Url = "https://stm19.srvstm.com:7080/stream",
                        Enabled = true
                    },
                    new TransmissionLinkSettings
                    {
                        Name = "Reserva 1",
                        Url = string.Empty,
                        Enabled = false
                    },
                    new TransmissionLinkSettings
                    {
                        Name = "Reserva 2",
                        Url = string.Empty,
                        Enabled = false
                    }
                ]
            },
            Audio = new AudioSettings
            {
                Backend = "DirectSound",
                OutputDeviceId = string.Empty,
                OutputDeviceName = string.Empty,
                WaveOutDeviceNumber = -1
            },
            Service = new ServiceSettings
            {
                Name = "GTFRXTlink",
                DisplayName = "GTF RX Tlink",
                Description = "Radio stream receiver service"
            },
            Console = new ConsoleSettings
            {
                LicensedTo = "KBUM 102,7 FM"
            }
        };
    }

    public AppConfig Clone()
    {
        var json = JsonSerializer.Serialize(this, ConfigService.JsonOptions);
        return JsonSerializer.Deserialize<AppConfig>(json, ConfigService.JsonOptions) ?? CreateDefault();
    }
}

internal sealed class StreamSettings
{
    public string Url { get; set; } = string.Empty;
    public List<TransmissionLinkSettings> Links { get; set; } = [];
    public bool AutoPlay { get; set; } = true;
    public bool ReconnectEnabled { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int BufferSeconds { get; set; } = 20;
    public int PrebufferSeconds { get; set; } = 5;
    public int BufferUnderrunFailSeconds { get; set; } = 3;
    public int PrimaryRetrySeconds { get; set; } = 30;
    public int LinkHealthTimeoutSeconds { get; set; } = 5;
}

internal sealed class TransmissionLinkSettings
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

internal sealed class AudioSettings
{
    public string Backend { get; set; } = "DirectSound";
    public string OutputDeviceId { get; set; } = string.Empty;
    public string OutputDeviceName { get; set; } = string.Empty;
    public int WaveOutDeviceNumber { get; set; } = -1;
}

internal sealed class ServiceSettings
{
    public string Name { get; set; } = "GTFRXTlink";
    public string DisplayName { get; set; } = "GTF RX Tlink";
    public string Description { get; set; } = "Radio stream receiver service";
}

internal sealed class ConsoleSettings
{
    public string LicensedTo { get; set; } = "KBUM 102,7 FM";
}
