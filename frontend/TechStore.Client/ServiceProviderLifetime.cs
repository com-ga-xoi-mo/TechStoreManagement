namespace TechStore.Client;

internal sealed class ServiceProviderLifetime(IServiceProvider services) : IDisposable, IAsyncDisposable
{
    private IServiceProvider? _services = services;

    public IServiceProvider Services =>
        Volatile.Read(ref _services) ?? throw new ObjectDisposedException(nameof(ServiceProviderLifetime));

    public void Dispose()
    {
        var services = Interlocked.Exchange(ref _services, null);
        (services as IDisposable)?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        var services = Interlocked.Exchange(ref _services, null);
        if (services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (services is IDisposable disposable)
            disposable.Dispose();
    }
}
