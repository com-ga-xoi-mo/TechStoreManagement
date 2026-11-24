using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Orders;

public abstract partial class ReturnWorkspaceViewModel(ISessionStore session) : PageViewModel
{
    private readonly long _generation = session.Current.Generation;
    private bool _subscribed;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string message = string.Empty;
    protected string? CurrentRole => session.Current.Role;
    public bool CanInteract => !IsBusy && session.Current.Generation == _generation && session.Current.Status == SessionStatus.Active;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanInteract));

    protected override bool ManagesLoadState => true;
    protected virtual bool HasLoadedContent => true;

    protected sealed override async Task<bool> LoadContentAsync(CancellationToken token)
    {
        if (!_subscribed)
        {
            session.SessionChanged += SessionChanged;
            _subscribed = true;
        }
        await ExecuteAsync(LoadAsync, loading: true);
        return IsContent;
    }

    protected abstract Task LoadAsync(CancellationToken token);
    protected abstract void ClearPage();

    protected Task ExecuteAsync(Func<CancellationToken, Task> action, bool loading = false)
    {
        if (!CanInteract) return Task.CompletedTask;
        IsBusy = true;
        Message = string.Empty;
        return RunTrackedRequestAsync(async token =>
        {
            if (loading)
            {
                ErrorMessage = string.Empty;
                ContentState = PageContentState.Loading;
            }
            try
            {
                await action(token);
                token.ThrowIfCancellationRequested();
                if (loading) ContentState = HasLoadedContent ? PageContentState.Content : PageContentState.Empty;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Message = ex is ApiException api ? OrderApiErrors.Describe(api, "Phiếu trả hàng") : GetSafeErrorMessage(ex);
                if (loading)
                {
                    ErrorMessage = Message;
                    ContentState = PageContentState.Error;
                }
            }
            finally { IsBusy = false; }
        });
    }

    protected static bool IsUncertain(ApiException error) => error.Kind != ApiErrorKind.Http || error.StatusCode >= 500;

    private void SessionChanged(object? sender, SessionChangedEventArgs args)
    {
        if (args.Generation == _generation && args.Status == SessionStatus.Active) return;
        CancelTrackedRequests();
        ContentState = PageContentState.Idle;
        ErrorMessage = string.Empty;
        ClearPage();
        Message = string.Empty;
        OnPropertyChanged(nameof(CanInteract));
    }

    protected override void OnDeactivated() => session.SessionChanged -= SessionChanged;
}
