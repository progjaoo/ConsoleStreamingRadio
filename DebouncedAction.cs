internal sealed class DebouncedAction : IDisposable
{
    private readonly object _sync = new();
    private readonly TimeSpan _delay;
    private readonly Action _action;
    private Timer? _timer;
    private bool _disposed;

    public DebouncedAction(TimeSpan delay, Action action)
    {
        _delay = delay;
        _action = action;
    }

    public void Signal()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _timer?.Dispose();
            _timer = new Timer(_ => Execute(), null, _delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Execute()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
        }

        try
        {
            _action();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Falha ao processar alteracao do arquivo de configuracao.");
        }
    }
}
