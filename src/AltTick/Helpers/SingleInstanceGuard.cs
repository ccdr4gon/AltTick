namespace AltTick.Helpers;

internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _hasHandle;

    public SingleInstanceGuard(string appName)
    {
        _mutex = new Mutex(false, $"Global\\{appName}");
    }

    public bool TryAcquire()
    {
        try
        {
            _hasHandle = _mutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            _hasHandle = true;
        }
        return _hasHandle;
    }

    public void Dispose()
    {
        if (_hasHandle)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
