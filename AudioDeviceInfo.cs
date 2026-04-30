internal sealed record AudioDeviceInfo(
    string Backend,
    string Id,
    string Name,
    bool IsDefault,
    int WaveOutDeviceNumber);
