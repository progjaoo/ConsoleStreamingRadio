internal sealed class DelegateDisposable : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public DelegateDisposable(Action dispose)
    {
        _dispose = dispose;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispose();
    }
}