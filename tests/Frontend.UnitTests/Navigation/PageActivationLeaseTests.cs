using TechStore.Client.Services.Navigation;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class PageActivationLeaseTests
{
    [Fact]
    public async Task Deactivate_CancelsImmediatelyButDefersScopeDisposalUntilWorkCompletes()
    {
        var work = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = 0;
        var lease = new PageActivationLease(() =>
        {
            Interlocked.Increment(ref disposed);
            return ValueTask.CompletedTask;
        });
        lease.Start(token =>
        {
            token.Register(() => canceled.TrySetResult());
            return work.Task;
        });

        var cleanup = lease.Deactivate();
        await canceled.Task;

        Assert.False(cleanup.IsCompleted);
        Assert.Equal(0, disposed);

        work.SetResult();
        await cleanup;

        Assert.Equal(1, disposed);
        Assert.Same(cleanup, lease.Deactivate());
    }

    [Fact]
    public async Task Deactivate_ObservesActivationFailureAndStillDisposesScopeOnce()
    {
        var disposed = 0;
        Exception? observed = null;
        var lease = new PageActivationLease(() =>
        {
            disposed++;
            return ValueTask.CompletedTask;
        });
        lease.ActivationFailed += (_, exception) => observed = exception;
        lease.Start(_ => Task.FromException(new InvalidOperationException("load failed")));

        await lease.Deactivate();
        await lease.DisposeAsync();

        Assert.Equal(1, disposed);
        Assert.IsType<InvalidOperationException>(observed);
    }

    [Fact]
    public async Task Start_IsIdempotentPerActivation()
    {
        var starts = 0;
        var lease = new PageActivationLease(() => ValueTask.CompletedTask);
        lease.Start(_ => { starts++; return Task.CompletedTask; });
        lease.Start(_ => { starts++; return Task.CompletedTask; });

        await lease.Deactivate();

        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task ViewModelLease_DeactivatesAndDetachesImmediately_ThenDisposesAfterUncooperativeLoad()
    {
        var work = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = 0;
        var detached = 0;
        var viewModel = new WaitingPageViewModel(work.Task);
        var lease = viewModel.CreateActivationLease(
            () => { disposed++; return ValueTask.CompletedTask; },
            detachSubscriptions: [() => detached++]);
        lease.Start(viewModel.ActivateAsync);

        var cleanup = lease.Deactivate();

        Assert.Equal(1, detached);
        Assert.Equal(0, disposed);
        Assert.False(cleanup.IsCompleted);
        work.SetResult(true);
        await cleanup;

        Assert.Equal(PageContentState.Loading, viewModel.ContentState);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task ViewModelLease_WaitsForPostActivationQueryBeforeDisposingScope()
    {
        var loadFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = 0;
        var viewModel = new TrackedRequestPageViewModel(loadFinished.Task);
        var lease = viewModel.CreateActivationLease(() =>
        {
            Interlocked.Increment(ref disposed);
            return ValueTask.CompletedTask;
        });

        lease.Start(viewModel.ActivateAsync);
        loadFinished.SetResult();
        await viewModel.ActivationLoad;

        var request = viewModel.StartQuery(query.Task, token => token.Register(() => queryCanceled.TrySetResult()));
        var cleanup = lease.Deactivate();
        await queryCanceled.Task;

        Assert.False(cleanup.IsCompleted);
        Assert.Equal(0, disposed);

        query.SetResult(); // Models a transport that finishes after its cancellation was requested.
        Assert.False(await request);
        await cleanup;

        Assert.Equal(1, disposed);
    }

    [Fact]
    public async Task WindowShutdown_CancelsAndWaitsForTrackedWorkWithoutBlockingTheSignal()
    {
        using var window = new CancellationTokenSource();
        var load = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = 0;
        var viewModel = new TrackedRequestPageViewModel(load.Task);
        var lease = viewModel.CreateActivationLease(
            () => { Interlocked.Increment(ref disposed); return ValueTask.CompletedTask; },
            window.Token);
        lease.Start(viewModel.ActivateAsync);
        load.SetResult();
        await viewModel.ActivationLoad;
        _ = viewModel.StartQuery(query.Task, token => token.Register(() => canceled.TrySetResult()));

        window.Cancel();
        await canceled.Task;

        Assert.Equal(0, disposed);
        query.SetResult();
        await lease.Deactivate();
        Assert.Equal(1, disposed);
    }

    private sealed class WaitingPageViewModel(Task<bool> work) : PageViewModel
    {
        protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => work;
    }

    private sealed class TrackedRequestPageViewModel(Task load) : PageViewModel
    {
        public Task ActivationLoad { get; private set; } = Task.CompletedTask;

        protected override async Task<bool> LoadContentAsync(CancellationToken cancellationToken)
        {
            await load;
            return true;
        }

        public Task<bool> StartQuery(Task work, Action<CancellationToken> observeCancellation) =>
            RunTrackedRequestAsync(async token =>
            {
                observeCancellation(token);
                await work;
            });

        public new Task ActivateAsync(CancellationToken windowLifetime = default) =>
            ActivationLoad = base.ActivateAsync(windowLifetime);
    }
}
