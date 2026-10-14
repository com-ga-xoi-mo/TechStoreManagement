using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Services.Navigation;

namespace TechStore.Client.ViewModels.Pages;

/// <summary>Shared observable load state. Implementations provide data through repositories, never UI controls.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    private readonly object _workGate = new();
    private readonly HashSet<TrackedWork> _trackedWork = [];
    private CancellationTokenSource? _activation;
    private CancellationTokenSource? _latestRequest;
    private Task _activeLoad = Task.CompletedTask;
    private long _generation;
    private long _latestRequestGeneration;
    private CancellationToken _windowLifetime;
    private bool _deactivated;

    private PageContentState _contentState;

    public PageContentState ContentState
    {
        get => _contentState;
        protected set
        {
            if (SetProperty(ref _contentState, value))
            {
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsContent));
                OnPropertyChanged(nameof(IsError));
                OnPropertyChanged(nameof(ContentStateDescription));
                RetryCommand.NotifyCanExecuteChanged();
                OnContentStateChanged();
            }
        }
    }

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool IsLoading => ContentState == PageContentState.Loading;
    public bool IsIdle => ContentState == PageContentState.Idle;
    public bool IsEmpty => ContentState == PageContentState.Empty;
    public bool IsContent => ContentState == PageContentState.Content;
    public bool IsError => ContentState == PageContentState.Error;
    public string ContentStateDescription => ContentState switch
    {
        PageContentState.Idle => "Chưa bắt đầu",
        PageContentState.Loading => "Đang tải",
        PageContentState.Empty => "Không có dữ liệu",
        PageContentState.Content => "Đã tải nội dung",
        PageContentState.Error => "Có lỗi",
        _ => "Không xác định"
    };
    protected CancellationToken ActivationToken => _activation?.Token ?? _windowLifetime;

    /// <summary>Override when the page coordinates state for replaceable requests itself.</summary>
    protected virtual bool ManagesLoadState => false;

    public PageActivationLease CreateActivationLease(
        Func<ValueTask> disposeScope,
        CancellationToken windowLifetime = default,
        params Action[] detachSubscriptions) =>
        new(disposeScope, windowLifetime, WaitForTrackedWorkAsync, [Deactivate, .. detachSubscriptions]);

    public Task ActivateAsync(CancellationToken windowLifetime = default)
    {
        _windowLifetime = windowLifetime;
        return StartActivation();
    }

    protected Task ReloadAsync() => StartActivation();

    private Task StartActivation()
    {
        if (_activation is { IsCancellationRequested: false } activation)
        {
            if (!_activeLoad.IsCompleted) return _activeLoad;
            var retryGeneration = ++_generation;
            return _activeLoad = LoadGenerationAsync(retryGeneration, activation.Token);
        }

        lock (_workGate)
        {
            if (_deactivated) throw new OperationCanceledException("The page activation has ended.");
            var activationSource = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime);
            _activation = activationSource;
            var generation = ++_generation;
            return _activeLoad = LoadGenerationAsync(generation, activationSource.Token);
        }
    }

    public void Deactivate()
    {
        CancellationTokenSource? activation;
        TrackedWork[] outstanding;
        lock (_workGate)
        {
            if (_deactivated) return;
            _deactivated = true;
            activation = _activation;
            _activation = null;
            ++_generation;
            ++_latestRequestGeneration;
            _latestRequest = null;
            outstanding = _trackedWork.ToArray();
        }

        CancelSafely(activation);
        foreach (var work in outstanding) CancelSafely(work.Cancellation);
        OnDeactivated();
        if (activation is not null)
            _ = _activeLoad.ContinueWith(
                _ => activation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync() => RetryContentAsync();

    protected virtual Task RetryContentAsync() => StartActivation();

    protected virtual void OnDeactivated() { }
    protected virtual void OnContentStateChanged() { }
    protected virtual void OnLatestRequestFailed(Exception exception) { }

    protected void CancelLatestRequest()
    {
        CancellationTokenSource? latest;
        lock (_workGate)
        {
            ++_latestRequestGeneration;
            latest = _latestRequest;
            _latestRequest = null;
        }
        CancelSafely(latest);
    }

    protected void CancelTrackedRequests()
    {
        TrackedWork[] outstanding;
        lock (_workGate)
        {
            ++_latestRequestGeneration;
            _latestRequest = null;
            outstanding = _trackedWork.ToArray();
        }
        foreach (var work in outstanding) CancelSafely(work.Cancellation);
    }

    protected virtual Task<bool> LoadContentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(false);

    private bool CanRetry() => ContentState == PageContentState.Error;

    /// <summary>Runs replaceable work and commits only if it still owns the activation.</summary>
    protected Task<bool> RunLatestRequestAsync<T>(
        Func<CancellationToken, Task<T>> request,
        Action<T> commit,
        Func<T, bool> hasContent,
        Func<bool>? hasRetainedContent = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(hasContent);

        TrackedWork work;
        CancellationTokenSource? previous;
        long generation;
        lock (_workGate)
        {
            ObjectDisposedException.ThrowIf(_deactivated, this);
            previous = _latestRequest;
            generation = ++_latestRequestGeneration;
            work = new TrackedWork(CancellationTokenSource.CreateLinkedTokenSource(ActivationToken));
            _latestRequest = work.Cancellation;
            _trackedWork.Add(work);
        }

        CancelSafely(previous);
        ErrorMessage = string.Empty;
        if (!ManagesLoadState) ContentState = PageContentState.Loading;
        _ = CompleteLatestRequestAsync(work, generation, request, commit, hasContent, hasRetainedContent);
        return work.Completion.Task;
    }

    /// <summary>Tracks independent work such as a category lookup under this page's activation scope.</summary>
    protected Task<bool> RunTrackedRequestAsync(Func<CancellationToken, Task> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TrackedWork work;
        lock (_workGate)
        {
            ObjectDisposedException.ThrowIf(_deactivated, this);
            work = new TrackedWork(CancellationTokenSource.CreateLinkedTokenSource(ActivationToken));
            _trackedWork.Add(work);
        }
        _ = CompleteTrackedRequestAsync(work, request);
        return work.Completion.Task;
    }

    protected virtual string GetSafeErrorMessage(Exception exception) => "Không thể tải nội dung. Hãy thử lại.";

    private async Task CompleteLatestRequestAsync<T>(
        TrackedWork work,
        long generation,
        Func<CancellationToken, Task<T>> request,
        Action<T> commit,
        Func<T, bool> hasContent,
        Func<bool>? hasRetainedContent)
    {
        try
        {
            var result = await request(work.Cancellation.Token);
            var hasResultContent = hasContent(result);
            if (TryCommitIfLatest(work, generation, () =>
            {
                commit(result);
                if (!ManagesLoadState)
                    ContentState = hasResultContent ? PageContentState.Content : PageContentState.Empty;
            })) work.Completion.TrySetResult(hasResultContent);
            else work.Completion.TrySetResult(false);
        }
        catch (OperationCanceledException) when (work.Cancellation.IsCancellationRequested)
        {
            if (!ManagesLoadState)
                TryCommitIfLatest(work, generation, () =>
                    ContentState = hasRetainedContent?.Invoke() == true ? PageContentState.Content : PageContentState.Idle);
            work.Completion.TrySetResult(false);
        }
        catch (Exception exception)
        {
            TryCommitIfLatest(work, generation, () =>
            {
                OnLatestRequestFailed(exception);
                ErrorMessage = GetSafeErrorMessage(exception);
                if (!ManagesLoadState) ContentState = PageContentState.Error;
            });
            work.Completion.TrySetResult(false);
        }
        finally
        {
            CompleteTrackedWork(work);
        }
    }

    private async Task CompleteTrackedRequestAsync(TrackedWork work, Func<CancellationToken, Task> request)
    {
        try
        {
            await request(work.Cancellation.Token);
            work.Completion.TrySetResult(!work.Cancellation.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (work.Cancellation.IsCancellationRequested)
        {
            work.Completion.TrySetResult(false);
        }
        catch (Exception)
        {
            work.Completion.TrySetResult(false);
        }
        finally
        {
            CompleteTrackedWork(work);
        }
    }

    private bool TryCommitIfLatest(TrackedWork work, long generation, Action commit)
    {
        lock (_workGate)
        {
            if (_deactivated || generation != _latestRequestGeneration
                || !ReferenceEquals(_latestRequest, work.Cancellation)
                || work.Cancellation.IsCancellationRequested)
                return false;
            commit();
            return true;
        }
    }

    private void CompleteTrackedWork(TrackedWork work)
    {
        lock (_workGate)
        {
            _trackedWork.Remove(work);
            if (ReferenceEquals(_latestRequest, work.Cancellation)) _latestRequest = null;
        }
        work.Cancellation.Dispose();
    }

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        catch (AggregateException) { }
    }

    private async Task WaitForTrackedWorkAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (_workGate) pending = _trackedWork.Select(work => (Task)work.Completion.Task).ToArray();
            if (pending.Length == 0) return;
            await Task.WhenAll(pending);
        }
    }

    private async Task LoadGenerationAsync(long generation, CancellationToken cancellationToken)
    {
        if (!ManagesLoadState && IsCurrent(generation))
        {
            ErrorMessage = string.Empty;
            ContentState = PageContentState.Loading;
        }

        try
        {
            var hasContent = await LoadContentAsync(cancellationToken);
            if (!ManagesLoadState && IsCurrent(generation))
            {
                ContentState = hasContent ? PageContentState.Content : PageContentState.Empty;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!ManagesLoadState && IsCurrent(generation))
            {
                ContentState = PageContentState.Idle;
            }
        }
        catch (Exception exception)
        {
            if (!ManagesLoadState && IsCurrent(generation))
            {
                ErrorMessage = GetSafeErrorMessage(exception);
                ContentState = PageContentState.Error;
            }
        }
    }

    private bool IsCurrent(long generation) =>
        generation == _generation && _activation is { IsCancellationRequested: false };

    private sealed class TrackedWork(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
