using TechStore.Client.Services.Dialog;

namespace TechStore.Client.UnitTests.Dialogs;

public sealed class DialogServiceTests
{
    private static readonly DialogRequest Request = new("Xác nhận", "Tiếp tục?", "Tiếp tục", "Hủy");

    [Fact]
    public async Task ConcurrentRequests_ArePresentedOneAtATimeInFifoOrder()
    {
        var presenter = new ControlledPresenter();
        await using var service = new DialogService(presenter);

        var first = service.ConfirmAsync(Request);
        var firstPresentation = await presenter.WaitForPresentationAsync();
        var second = service.InformAsync(Request);
        Assert.Single(presenter.Presented);

        firstPresentation.Completion.SetResult(DialogResult.Confirmed);
        Assert.Equal(DialogResult.Confirmed, await first);
        var secondPresentation = await presenter.WaitForPresentationAsync();
        secondPresentation.Completion.SetResult(DialogResult.Dismissed);

        Assert.Equal(DialogResult.Dismissed, await second);
        Assert.Equal([true, false], presenter.Presented.Select(item => item.IsConfirmation));
    }

    [Fact]
    public async Task UserSelectingCancel_ReturnsDecisionInsteadOfTaskCancellation()
    {
        var presenter = new ControlledPresenter();
        await using var service = new DialogService(presenter);

        var request = service.ConfirmAsync(Request);
        var shown = await presenter.WaitForPresentationAsync();
        shown.Completion.SetResult(DialogResult.Cancelled);

        Assert.Equal(DialogResult.Cancelled, await request);
    }

    [Fact]
    public async Task QueuedCancellation_SkipsOnlyThatRequestAndContinuesQueue()
    {
        var presenter = new ControlledPresenter();
        await using var service = new DialogService(presenter);
        using var cancellation = new CancellationTokenSource();

        var first = service.InformAsync(Request);
        var firstPresentation = await presenter.WaitForPresentationAsync();
        var canceled = service.ConfirmAsync(Request, cancellation.Token);
        var third = service.InformAsync(Request);
        cancellation.Cancel();
        firstPresentation.Completion.SetResult(DialogResult.Dismissed);
        await first;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        var thirdPresentation = await presenter.WaitForPresentationAsync();
        thirdPresentation.Completion.SetResult(DialogResult.Dismissed);
        await third;

        Assert.Equal(2, presenter.Presented.Count);
    }

    [Fact]
    public async Task VisibleCancellation_ClosesTheActiveRequest()
    {
        var presenter = new ControlledPresenter();
        await using var service = new DialogService(presenter);
        using var cancellation = new CancellationTokenSource();

        var request = service.ConfirmAsync(Request, cancellation.Token);
        var presentation = await presenter.WaitForPresentationAsync();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(presentation.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task MissingRoot_FailsPromptlyAndDoesNotQueueDialog()
    {
        var presenter = new ControlledPresenter { IsReady = false };
        await using var service = new DialogService(presenter);

        await Assert.ThrowsAsync<DialogUnavailableException>(() => service.InformAsync(Request));
        Assert.Empty(presenter.Presented);
    }

    [Fact]
    public async Task Shutdown_CancelsVisibleAndQueuedRequests_AndRejectsNewWork()
    {
        var presenter = new ControlledPresenter();
        var service = new DialogService(presenter);
        var active = service.InformAsync(Request);
        var shown = await presenter.WaitForPresentationAsync();
        var queued = service.ConfirmAsync(Request);

        await service.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.True(shown.CancellationToken.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => { _ = service.InformAsync(Request); });
    }

    private sealed class ControlledPresenter : IDialogPresenter
    {
        private readonly object _gate = new();
        private readonly List<PresentedDialog> _presented = [];
        private readonly Queue<PresentedDialog> _available = new();
        private readonly Queue<TaskCompletionSource<PresentedDialog>> _waiters = new();

        public bool IsReady { get; set; } = true;
        public IReadOnlyList<PresentedDialog> Presented { get { lock (_gate) return _presented.ToArray(); } }

        public Task<DialogResult> PresentAsync(DialogRequest request, bool isConfirmation, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var shown = new PresentedDialog(isConfirmation, cancellationToken, completion);
            lock (_gate)
            {
                _presented.Add(shown);
                if (_waiters.TryDequeue(out var waiter)) waiter.TrySetResult(shown);
                else _available.Enqueue(shown);
            }
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return completion.Task;
        }

        public Task<PresentedDialog> WaitForPresentationAsync()
        {
            lock (_gate)
            {
                if (_available.TryDequeue(out var shown)) return Task.FromResult(shown);
                var waiter = new TaskCompletionSource<PresentedDialog>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue(waiter);
                return waiter.Task;
            }
        }

    }

    private sealed record PresentedDialog(
        bool IsConfirmation,
        CancellationToken CancellationToken,
        TaskCompletionSource<DialogResult> Completion);
}
