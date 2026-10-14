namespace TechStore.Client.Services.Window;

public interface IWindowLifetime
{
    CancellationToken Token { get; }
    void Close();
}

public sealed class WindowLifetime : IWindowLifetime, IDisposable
{
    private readonly CancellationTokenSource _closed = new();

    public CancellationToken Token => _closed.Token;

    public void Close()
    {
        if (!_closed.IsCancellationRequested)
            _closed.Cancel();
    }

    public void Dispose() => _closed.Dispose();
}
