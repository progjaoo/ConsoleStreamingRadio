using System.Runtime.InteropServices;

internal sealed class WindowsServiceHost
{
    private const int ServiceWin32OwnProcess = 0x00000010;
    private const int ServiceStopped = 0x00000001;
    private const int ServiceStartPending = 0x00000002;
    private const int ServiceStopPending = 0x00000003;
    private const int ServiceRunning = 0x00000004;
    private const int ServiceAcceptStop = 0x00000001;
    private const int ServiceAcceptShutdown = 0x00000004;
    private const int ControlStop = 0x00000001;
    private const int ControlInterrogate = 0x00000004;
    private const int ControlShutdown = 0x00000005;
    private const int NoError = 0;

    private readonly string _serviceName;
    private readonly Func<CancellationToken, Task> _runAsync;
    private readonly ManualResetEventSlim _stopped = new(false);
    private CancellationTokenSource? _cts;
    private IntPtr _statusHandle;
    private ServiceMainDelegate? _serviceMainDelegate;
    private ServiceControlHandlerEx? _handlerDelegate;

    public WindowsServiceHost(string serviceName, Func<CancellationToken, Task> runAsync)
    {
        _serviceName = serviceName;
        _runAsync = runAsync;
    }

    public int Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            AppLogger.Error("Modo servico disponivel apenas no Windows.");
            return 1;
        }

        _serviceMainDelegate = ServiceMain;

        var serviceTable = new[]
        {
            new ServiceTableEntry
            {
                ServiceName = _serviceName,
                ServiceProc = _serviceMainDelegate
            },
            new ServiceTableEntry()
        };

        if (StartServiceCtrlDispatcher(serviceTable))
        {
            return 0;
        }

        var error = Marshal.GetLastWin32Error();
        AppLogger.Error($"Falha ao registrar processo no Service Control Manager. Codigo: {error}");
        return error;
    }

    private void ServiceMain(int argc, IntPtr argv)
    {
        _handlerDelegate = ServiceControlHandler;
        _statusHandle = RegisterServiceCtrlHandlerEx(_serviceName, _handlerDelegate, IntPtr.Zero);

        if (_statusHandle == IntPtr.Zero)
        {
            AppLogger.Error($"RegisterServiceCtrlHandlerEx falhou. Codigo: {Marshal.GetLastWin32Error()}");
            return;
        }

        SetStatus(ServiceStartPending, 0, 3000);
        _cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                SetStatus(ServiceRunning);
                await _runAsync(_cts.Token);
                SetStatus(ServiceStopped);
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Servico finalizado com erro.");
                SetStatus(ServiceStopped, 1);
            }
            finally
            {
                _stopped.Set();
            }
        });

        _stopped.Wait();
    }

    private int ServiceControlHandler(int control, int eventType, IntPtr eventData, IntPtr context)
    {
        switch (control)
        {
            case ControlStop:
            case ControlShutdown:
                SetStatus(ServiceStopPending, 0, 5000);
                _cts?.Cancel();
                return NoError;

            case ControlInterrogate:
                return NoError;

            default:
                return NoError;
        }
    }

    private void SetStatus(int state, int win32ExitCode = 0, int waitHint = 0)
    {
        if (_statusHandle == IntPtr.Zero)
        {
            return;
        }

        var status = new ServiceStatus
        {
            ServiceType = ServiceWin32OwnProcess,
            CurrentState = state,
            ControlsAccepted = state == ServiceRunning
                ? ServiceAcceptStop | ServiceAcceptShutdown
                : 0,
            Win32ExitCode = win32ExitCode,
            ServiceSpecificExitCode = 0,
            CheckPoint = state is ServiceStartPending or ServiceStopPending ? 1 : 0,
            WaitHint = waitHint
        };

        if (!SetServiceStatus(_statusHandle, ref status))
        {
            AppLogger.Error($"SetServiceStatus falhou. Codigo: {Marshal.GetLastWin32Error()}");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ServiceTableEntry
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ServiceName;
        public ServiceMainDelegate? ServiceProc;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public int ServiceType;
        public int CurrentState;
        public int ControlsAccepted;
        public int Win32ExitCode;
        public int ServiceSpecificExitCode;
        public int CheckPoint;
        public int WaitHint;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Unicode)]
    private delegate void ServiceMainDelegate(int argc, IntPtr argv);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ServiceControlHandlerEx(int control, int eventType, IntPtr eventData, IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartServiceCtrlDispatcher([In] ServiceTableEntry[] serviceStartTable);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr RegisterServiceCtrlHandlerEx(string serviceName, ServiceControlHandlerEx handler, IntPtr context);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetServiceStatus(IntPtr serviceStatusHandle, ref ServiceStatus serviceStatus);
}
