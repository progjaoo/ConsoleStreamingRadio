using System.Diagnostics;

internal static class WindowsServiceCommands
{
    private const string WinSwExecutableName = "GTF-RX-Tlink-Service.exe";

    public static int Install(AppConfig config)
    {
        if (TryRunWinSw("install", out var exitCode))
        {
            return exitCode;
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando instalacao antiga via sc.exe.");
        return InstallLegacy(config);
    }

    public static int Uninstall(AppConfig config)
    {
        if (TryRunWinSw("stop", out _))
        {
            return RunWinSw("uninstall");
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando remocao antiga via sc.exe.");
        StopLegacy(config);
        return RunSc("delete", config.Service.Name);
    }

    public static int Start(AppConfig config)
    {
        if (TryRunWinSw("start", out var exitCode))
        {
            return exitCode;
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando inicializacao antiga via sc.exe.");
        return StartLegacy(config);
    }

    public static int Stop(AppConfig config)
    {
        if (TryRunWinSw("stop", out var exitCode))
        {
            return exitCode;
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando parada antiga via sc.exe.");
        return StopLegacy(config);
    }

    public static int Restart(AppConfig config)
    {
        if (WinSwExists())
        {
            var stopExitCode = RunWinSw("stop");
            return stopExitCode != 0 ? stopExitCode : RunWinSw("start");
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando reinicio antigo via sc.exe.");
        if (!EnsureWindows())
        {
            return 1;
        }

        StopLegacy(config);
        Thread.Sleep(TimeSpan.FromSeconds(2));
        return StartLegacy(config);
    }

    public static int Status(AppConfig config)
    {
        if (TryRunWinSw("status", out var exitCode))
        {
            return exitCode;
        }

        Console.WriteLine("Wrapper WinSW nao encontrado na pasta do aplicativo. Usando consulta antiga via sc.exe.");
        return EnsureWindows() ? RunSc("query", config.Service.Name) : 1;
    }

    private static int InstallLegacy(AppConfig config)
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

    private static int StartLegacy(AppConfig config)
    {
        return EnsureWindows() ? RunSc("start", config.Service.Name) : 1;
    }

    private static int StopLegacy(AppConfig config)
    {
        return EnsureWindows() ? RunSc("stop", config.Service.Name) : 1;
    }

    private static bool WinSwExists()
    {
        return File.Exists(GetWinSwPath());
    }

    private static bool TryRunWinSw(string command, out int exitCode)
    {
        exitCode = 1;

        if (!WinSwExists())
        {
            return false;
        }

        exitCode = RunWinSw(command);
        return true;
    }

    private static int RunWinSw(string command)
    {
        if (!EnsureWindows())
        {
            return 1;
        }

        var processStartInfo = new ProcessStartInfo(GetWinSwPath(), command)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            Console.WriteLine("Nao foi possivel executar o wrapper WinSW.");
            return 1;
        }

        process.WaitForExit();
        WriteProcessOutput(process);
        return process.ExitCode;
    }

    private static string GetWinSwPath()
    {
        return Path.Combine(AppContext.BaseDirectory, WinSwExecutableName);
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
        WriteProcessOutput(process);
        return process.ExitCode;
    }

    private static void WriteProcessOutput(Process process)
    {
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
    }
}
