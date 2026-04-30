using System.Diagnostics;

internal static class WindowsServiceCommands
{
    public static int Install(AppConfig config)
    {
        if (!EnsureWindows())
        {
            return 1;
        }

        var exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Nao foi possivel identificar o caminho do EXE.");

        var binPath = $"\"{exePath}\" --service";
        var createExitCode = RunSc("create", config.Service.Name, "binPath=", binPath, "start=", "auto", "DisplayName=", config.Service.DisplayName);

        if (createExitCode != 0)
        {
            return createExitCode;
        }

        RunSc("description", config.Service.Name, config.Service.Description);
        Console.WriteLine("Servico instalado. Use --start-service ou o menu para iniciar.");
        return 0;
    }

    public static int Uninstall(AppConfig config)
    {
        if (!EnsureWindows())
        {
            return 1;
        }

        Stop(config);
        return RunSc("delete", config.Service.Name);
    }

    public static int Start(AppConfig config)
    {
        return EnsureWindows() ? RunSc("start", config.Service.Name) : 1;
    }

    public static int Stop(AppConfig config)
    {
        return EnsureWindows() ? RunSc("stop", config.Service.Name) : 1;
    }

    public static int Restart(AppConfig config)
    {
        if (!EnsureWindows())
        {
            return 1;
        }

        Stop(config);
        Thread.Sleep(TimeSpan.FromSeconds(2));
        return Start(config);
    }

    public static int Status(AppConfig config)
    {
        return EnsureWindows() ? RunSc("query", config.Service.Name) : 1;
    }

    private static bool EnsureWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        Console.WriteLine("Comando disponivel apenas no Windows.");
        return false;
    }

    private static int RunSc(params string[] arguments)
    {
        var processStartInfo = new ProcessStartInfo("sc.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            Console.WriteLine("Nao foi possivel executar sc.exe.");
            return 1;
        }

        process.WaitForExit();

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.WriteLine(error.Trim());
        }

        return process.ExitCode;
    }
}
