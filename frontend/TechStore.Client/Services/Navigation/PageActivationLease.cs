namespace TechStore.Client.Services.Navigation;

/// <summary>
/// Owns one page activation and its DI scope. Navigation can deactivate immediately;
/// the scope is released after the activation task has stopped using its dependencies.
/// </summary>
public sealed class PageActivationLease : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationTokenRegistration _windowLifetimeRegistration;
    private readonly Func<ValueTask> _disposeScope;
    private readonly Func<Task>? _waitForAdditionalWork;
    private readonly Action[] _deactivateActions;
    private readonly object _gate = new();
    private Task _work = Task.CompletedTask;
    private Task? _cleanup;
    private bool _started;

    public PageActivationLease(
        Func<ValueTask> disposeScope,
        CancellationToken windowLifetime = default,
        params Action[] deactivateActions)
        : this(disposeScope, windowLifetime, null, deactivateActions)
    {
    }

    public PageActivationLease(
        Func<ValueTask> disposeScope,
        CancellationToken windowLifetime,
        Func<Task>? waitForAdditionalWork,
        params Action[] deactivateActions)
    {
        _disposeScope = disposeScope ?? throw new ArgumentNullException(nameof(disposeScope));
        _waitForAdditionalWork = waitForAdditionalWork;
        _deactivateActions = deactivateActions ?? [];
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(windowLifetime);
        _windowLifetimeRegistration = windowLifetime.CanBeCanceled
            ? windowLifetime.Register(static state => _ = ((PageActivationLease)state!).Deactivate(), this)
            : default;
    }

    public CancellationToken CancellationToken => _lifetime.Token;

    public event EventHandler<Exception>? ActivationFailed;
    public event EventHandler<Exception>? DeactivationFailed;

    public void Start(Func<CancellationToken, Task> activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_cleanup is not null, this);
            if (_started) return;
            _started = true;
            _work = InvokeActivationAsync(activate);
        }
    }

    /// <summary>Signals cancellation and schedules scope cleanup without waiting for non-cooperative work.</summary>
    public Task Deactivate()
    {
        lock (_gate)
        {
            if (_cleanup is not null) return _cleanup;
            try { _lifetime.Cancel(); }
            catch (ObjectDisposedException) { }
            catch (AggregateException) { }
            foreach (var action in _deactivateActions)
            {
                try { action(); }
                catch (Exception exception) { DeactivationFailed?.Invoke(this, exception); }
            }
            _cleanup = CleanupAfterWorkAsync(_work);
            return _cleanup;
        }
    }

    public ValueTask DisposeAsync() => new(Deactivate());

    private async Task InvokeActivationAsync(Func<CancellationToken, Task> activate)
    {
        try
        {
            await activate(_lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ActivationFailed?.Invoke(this, exception);
        }
    }

    private async Task CleanupAfterWorkAsync(Task work)
    {
        try
        {
            await work.ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (_waitForAdditionalWork is not null)
                    await _waitForAdditionalWork().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                await _disposeScope().ConfigureAwait(false);
                }
                finally
                {
                    _windowLifetimeRegistration.Dispose();
                    _lifetime.Dispose();
                }
            }
        }
    }
}
