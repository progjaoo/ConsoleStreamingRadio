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
                ReconnectDelaySeconds = 5
            },
            Audio = new AudioSettings
            {
                Backend = "Wasapi",
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
    public bool AutoPlay { get; set; } = true;
    public bool ReconnectEnabled { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 5;
}

internal sealed class AudioSettings
{
    public string Backend { get; set; } = "Wasapi";
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
