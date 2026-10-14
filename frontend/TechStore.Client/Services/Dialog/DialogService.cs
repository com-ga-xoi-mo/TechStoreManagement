using System.Threading.Channels;

namespace TechStore.Client.Services.Dialog;

/// <summary>Serializes modal requests for one window and binds every request to its caller/window lifetime.</summary>
public sealed class DialogService : IDialogService, IDisposable, IAsyncDisposable
{
    private readonly IDialogPresenter _presenter;
    private readonly Channel<DialogWork> _queue = Channel.CreateUnbounded<DialogWork>(new UnboundedChannelOptions
    {
        SingleReader = true,
        AllowSynchronousContinuations = false
    });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _pump;
    private int _disposed;

    public DialogService(IDialogPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _pump = PumpAsync();
    }

    public Task<DialogResult> ConfirmAsync(DialogRequest request, CancellationToken cancellationToken = default) =>
        Enqueue(request, isConfirmation: true, cancellationToken);

    public Task<DialogResult> InformAsync(DialogRequest request, CancellationToken cancellationToken = default) =>
        Enqueue(request, isConfirmation: false, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        BeginShutdown();
        await _pump.ConfigureAwait(false);
        _shutdown.Dispose();
    }

    public void Dispose() => BeginShutdown();

    private void BeginShutdown()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
    }

    private Task<DialogResult> Enqueue(
        DialogRequest request,
        bool isConfirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message) ||
            string.IsNullOrWhiteSpace(request.PrimaryButtonText))
            throw new ArgumentException("Dialog title, message and primary button label are required.", nameof(request));
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var work = new DialogWork(request, isConfirmation, cancellationToken);
        if (!_queue.Writer.TryWrite(work))
            work.Completion.TrySetCanceled(_shutdown.Token);
        return work.Completion.Task;
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (var work in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                if (work.CallerToken.IsCancellationRequested)
                {
                    work.Completion.TrySetCanceled(work.CallerToken);
                    continue;
                }

                if (!_presenter.IsReady)
                {
                    work.Completion.TrySetException(new DialogUnavailableException(
                        "The window dialog root is not ready."));
                    continue;
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(work.CallerToken, _shutdown.Token);
                try
                {
                    var result = await _presenter.PresentAsync(
                        work.Request, work.IsConfirmation, linked.Token).ConfigureAwait(false);
                    work.Completion.TrySetResult(result);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    work.Completion.TrySetCanceled(work.CallerToken.IsCancellationRequested
                        ? work.CallerToken
                        : _shutdown.Token);
                }
                catch (Exception exception)
                {
                    work.Completion.TrySetException(exception);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            while (_queue.Reader.TryRead(out var pending))
                pending.Completion.TrySetCanceled(_shutdown.Token);
        }
    }

    private sealed class DialogWork(DialogRequest request, bool isConfirmation, CancellationToken callerToken)
    {
        public DialogRequest Request { get; } = request;
        public bool IsConfirmation { get; } = isConfirmation;
        public CancellationToken CallerToken { get; } = callerToken;
        public TaskCompletionSource<DialogResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
