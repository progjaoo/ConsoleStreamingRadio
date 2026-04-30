using NAudio.CoreAudioApi;
using NAudio.Wave;

internal sealed class AudioDeviceService
{
    public IReadOnlyList<AudioDeviceInfo> ListOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        AddWasapiDevices(devices);
        AddWaveOutDevices(devices);

        return devices;
    }

    public IWavePlayer CreateOutput(AudioSettings settings)
    {
        var backend = settings.Backend?.Trim() ?? "Wasapi";

        if (string.Equals(backend, "WaveOut", StringComparison.OrdinalIgnoreCase))
        {
            return CreateWaveOut(settings);
        }

        try
        {
            return CreateWasapi(settings);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or DllNotFoundException)
        {
            AppLogger.Error($"Falha ao abrir saida WASAPI. Usando WaveOut padrao. Detalhe: {ex.Message}");
            return CreateWaveOut(new AudioSettings());
        }
    }

    public AudioDeviceInfo? FindByMenuIndex(int oneBasedIndex)
    {
        var devices = ListOutputDevices();
        var zeroBasedIndex = oneBasedIndex - 1;

        if (zeroBasedIndex < 0 || zeroBasedIndex >= devices.Count)
        {
            return null;
        }

        return devices[zeroBasedIndex];
    }

    private static void AddWasapiDevices(ICollection<AudioDeviceInfo> devices)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultId = string.Empty;

            try
            {
                defaultId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
            {
                AppLogger.Error($"Nao foi possivel identificar a saida padrao WASAPI: {ex.Message}");
            }

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add(new AudioDeviceInfo(
                    "Wasapi",
                    device.ID,
                    device.FriendlyName,
                    string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase),
                    -1));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or DllNotFoundException)
        {
            AppLogger.Error($"Listagem WASAPI indisponivel neste ambiente: {ex.Message}");
        }
    }

    private static void AddWaveOutDevices(ICollection<AudioDeviceInfo> devices)
    {
        devices.Add(new AudioDeviceInfo(
            "WaveOut",
            "waveout:default",
            "Saida padrao do Windows (WaveOut)",
            false,
            -1));
    }

    private static IWavePlayer CreateWasapi(AudioSettings settings)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = ResolveWasapiDevice(enumerator, settings);

        AppLogger.Info($"Saida de audio: {device.FriendlyName} (WASAPI)");
        return new WasapiOut(device, AudioClientShareMode.Shared, false, 200);
    }

    private static MMDevice ResolveWasapiDevice(MMDeviceEnumerator enumerator, AudioSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputDeviceId))
        {
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (string.Equals(device.ID, settings.OutputDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return device;
                }
            }

            AppLogger.Error($"Placa configurada nao encontrada: {settings.OutputDeviceName} ({settings.OutputDeviceId}). Usando saida padrao.");
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    private static IWavePlayer CreateWaveOut(AudioSettings settings)
    {
        var deviceNumber = settings.WaveOutDeviceNumber >= 0
            ? settings.WaveOutDeviceNumber
            : -1;

        AppLogger.Info(deviceNumber >= 0
            ? $"Saida de audio: WaveOut #{deviceNumber}"
            : "Saida de audio: padrao do Windows (WaveOut)");

        return new WaveOutEvent
        {
            DeviceNumber = deviceNumber,
            DesiredLatency = 500
        };
    }
}
