internal static class AppLogger
{
    private static readonly object Sync = new();

    public static bool ConsoleEnabled { get; set; } = true;

    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");
    public static string LogPath => Path.Combine(LogDirectory, "radio-stream-player.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    public static void Error(Exception exception, string message)
    {
        Write("ERROR", $"{message} {exception}");
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";

        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never stop the stream player.
            }
        }

        if (!ConsoleEnabled)
        {
            return;
        }

        try
        {
            Console.WriteLine(line);
        }
        catch
        {
            // Ignore console failures when the process is hosted by SCM.
        }
    }
}
