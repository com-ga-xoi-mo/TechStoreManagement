using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.UnitTests.Navigation;

public sealed class PageViewModelTests
{
    [Fact]
    public async Task Activate_ReportsLoadingThenEmptyOrContent()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new TestPageViewModel(_ => completion.Task);

        var load = viewModel.ActivateAsync();
        Assert.Equal(PageContentState.Loading, viewModel.ContentState);
        completion.SetResult(true);
        await load;

        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.False(viewModel.IsLoading);

        var emptyViewModel = new TestPageViewModel(_ => Task.FromResult(false));
        await emptyViewModel.ActivateAsync();
        Assert.Equal(PageContentState.Empty, emptyViewModel.ContentState);
    }

    [Fact]
    public async Task Failure_ReportsSafeError_AndRetryDoesNotOverlap()
    {
        var started = 0;
        var retryCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new TestPageViewModel(_ =>
        {
            if (Interlocked.Increment(ref started) == 1)
                throw new InvalidOperationException("sensitive details");
            return retryCompletion.Task;
        });

        await viewModel.ActivateAsync();
        Assert.Equal(PageContentState.Error, viewModel.ContentState);
        Assert.DoesNotContain("sensitive", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.RetryCommand.CanExecute(null));

        var retry = viewModel.RetryCommand.ExecuteAsync(null);
        Assert.Equal(2, started);
        Assert.False(viewModel.RetryCommand.CanExecute(null));
        retryCompletion.SetResult(true);
        await retry;

        Assert.Equal(2, started);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task Deactivate_CancelsLoad_AndLateCompletionCannotChangeState()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedCancellation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new TestPageViewModel(token =>
        {
            token.Register(() => observedCancellation.TrySetResult());
            return completion.Task;
        });

        var load = viewModel.ActivateAsync();
        viewModel.Deactivate();
        await observedCancellation.Task;
        completion.SetResult(true);
        await load;

        Assert.Equal(PageContentState.Loading, viewModel.ContentState);
        Assert.NotEqual(PageContentState.Error, viewModel.ContentState);
    }

    [Fact]
    public async Task OldFailureAfterNewActivationCannotOverwriteNewState()
    {
        var oldCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldVm = new TestPageViewModel(_ => oldCompletion.Task);
        var oldLoad = oldVm.ActivateAsync();
        oldVm.Deactivate();

        var newVm = new TestPageViewModel(_ => Task.FromResult(true));
        await newVm.ActivateAsync();
        oldCompletion.SetException(new InvalidOperationException("old failure"));
        await oldLoad;

        Assert.Equal(PageContentState.Content, newVm.ContentState);
        Assert.Equal(PageContentState.Loading, oldVm.ContentState);
    }

    [Fact]
    public async Task LatestRequest_LateSuccessOrFailureCannotOverwriteNewerCommitOrError()
    {
        var viewModel = new LatestRequestPageViewModel();
        await viewModel.ActivateAsync();

        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = viewModel.RequestAsync(first.Task);
        var secondRequest = viewModel.RequestAsync(second.Task);

        second.SetResult("new");
        Assert.True(await secondRequest);
        Assert.Equal("new", viewModel.Value);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);

        first.SetException(new InvalidOperationException("stale failure"));
        Assert.False(await firstRequest);
        Assert.Equal("new", viewModel.Value);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LatestRequest_LateSuccessCannotReplaceTheNewestSnapshot()
    {
        var viewModel = new LatestRequestPageViewModel();
        await viewModel.ActivateAsync();
        var older = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var olderRequest = viewModel.RequestAsync(older.Task);
        var newerRequest = viewModel.RequestAsync(newer.Task);
        newer.SetResult("latest");
        Assert.True(await newerRequest);
        older.SetResult("stale");

        Assert.False(await olderRequest);
        Assert.Equal("latest", viewModel.Value);
        Assert.Equal(PageContentState.Content, viewModel.ContentState);
    }

    [Fact]
    public async Task LatestRequest_CancellationDoesNotBecomeError_AndRetryUsesNewRequest()
    {
        var viewModel = new LatestRequestPageViewModel();
        await viewModel.ActivateAsync();

        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequest = viewModel.RequestAsync(first.Task, token => token.Register(() => canceled.TrySetResult()));
        viewModel.CancelActivationWorkForTest();

        await canceled.Task;
        first.SetCanceled();
        Assert.False(await firstRequest);
        Assert.NotEqual(PageContentState.Error, viewModel.ContentState);
    }

    private sealed class TestPageViewModel(Func<CancellationToken, Task<bool>> load)
        : PageViewModel
    {
        protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) =>
            load(cancellationToken);
    }

    private sealed class LatestRequestPageViewModel : PageViewModel
    {
        public string? Value { get; private set; }

        protected override Task<bool> LoadContentAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> RequestAsync(Task<string> result, Action<CancellationToken>? observe = null) =>
            RunLatestRequestAsync(
                async token =>
                {
                    observe?.Invoke(token);
                    return await result;
                },
                value => Value = value,
                _ => true);

        public void CancelActivationWorkForTest() => Deactivate();
    }
}
