using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Services.Dialog;
using TechStore.Client.Services.Theme;

namespace TechStore.Client.Views.Shell;

/// <summary>WinUI boundary for the headless, serialized dialog service.</summary>
public sealed class ContentDialogPresenter(IThemePreferenceService themePreference) : IDialogPresenter
{
    private XamlRoot? _root;
    private DispatcherQueue? _dispatcher;
    private Func<Control?>? _focusFallback;
    private ContentDialog? _activeDialog;
    private int _ready;

    public bool IsReady => Volatile.Read(ref _ready) == 1;

    public void Attach(FrameworkElement root, Func<Control?> focusFallback)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(focusFallback);
        if (root.XamlRoot is null)
            throw new InvalidOperationException("A dialog root can only attach after the Shell is loaded.");

        _root = root.XamlRoot;
        _dispatcher = root.DispatcherQueue;
        _focusFallback = focusFallback;
        Volatile.Write(ref _ready, 1);
    }

    public void Detach()
    {
        Volatile.Write(ref _ready, 0);
        var dispatcher = _dispatcher;
        var activeDialog = _activeDialog;
        _root = null;
        _dispatcher = null;
        _focusFallback = null;

        if (activeDialog is not null && dispatcher is not null)
            dispatcher.TryEnqueue(() => TryHide(activeDialog));
    }

    public Task<DialogResult> PresentAsync(
        DialogRequest request,
        bool isConfirmation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = _root;
        var dispatcher = _dispatcher;
        if (!IsReady || root is null || dispatcher is null)
            throw new DialogUnavailableException("The Shell dialog root is not ready.");
        cancellationToken.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() => _ = ShowOnUiAsync(root, dispatcher, request, isConfirmation, cancellationToken, completion)))
            completion.TrySetException(new DialogUnavailableException("The Shell dispatcher is shutting down."));
        return completion.Task;
    }

    private async Task ShowOnUiAsync(
        XamlRoot root,
        DispatcherQueue dispatcher,
        DialogRequest request,
        bool isConfirmation,
        CancellationToken cancellationToken,
        TaskCompletionSource<DialogResult> completion)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return;
        }

        var previousFocus = FocusManager.GetFocusedElement(root);
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = request.Title,
            RequestedTheme = ToElementTheme(themePreference.Preference),
            Content = new TextBlock
            {
                Text = request.Message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = request.PrimaryButtonText,
            SecondaryButtonText = isConfirmation ? request.SecondaryButtonText ?? "Hủy" : string.Empty,
            DefaultButton = ContentDialogButton.Primary
        };

        _activeDialog = dialog;
        using var cancellationRegistration = cancellationToken.Register(() =>
            dispatcher.TryEnqueue(() => TryHide(dialog)));

        try
        {
            var result = await dialog.ShowAsync();
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            completion.TrySetResult(ToDialogResult(result, isConfirmation));
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
                completion.TrySetCanceled(cancellationToken);
            else
                completion.TrySetException(exception);
        }
        finally
        {
            if (ReferenceEquals(_activeDialog, dialog)) _activeDialog = null;
            RestoreFocus(root, previousFocus);
        }
    }

    /// <summary>
    /// Shows a dialog built by the caller under the same root, theme, single-dialog guard and focus recovery as
    /// the text dialogs. Returns <c>false</c> without showing anything when the Shell is not ready or another
    /// dialog is already open (WinUI allows only one at a time).
    /// </summary>
    public async Task<bool> TryShowAsync(Func<ContentDialog> create, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(create);
        var root = _root;
        var dispatcher = _dispatcher;
        if (!IsReady || root is null || dispatcher is null || cancellationToken.IsCancellationRequested) return false;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() => _ = ShowCustomOnUiAsync(root, dispatcher, create, cancellationToken, completion)))
            return false;
        return await completion.Task;
    }

    private async Task ShowCustomOnUiAsync(
        XamlRoot root,
        DispatcherQueue dispatcher,
        Func<ContentDialog> create,
        CancellationToken cancellationToken,
        TaskCompletionSource<bool> completion)
    {
        if (cancellationToken.IsCancellationRequested || _activeDialog is not null)
        {
            completion.TrySetResult(false);
            return;
        }

        var previousFocus = FocusManager.GetFocusedElement(root);
        ContentDialog dialog;
        try
        {
            dialog = create();
            dialog.XamlRoot = root;
            dialog.RequestedTheme = ToElementTheme(themePreference.Preference);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
            return;
        }

        _activeDialog = dialog;
        using var cancellationRegistration = cancellationToken.Register(() =>
            dispatcher.TryEnqueue(() => TryHide(dialog)));
        try
        {
            await dialog.ShowAsync();
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            if (ReferenceEquals(_activeDialog, dialog)) _activeDialog = null;
            RestoreFocus(root, previousFocus);
        }
    }

    private static ElementTheme ToElementTheme(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => ElementTheme.Light,
        ThemePreference.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private void RestoreFocus(XamlRoot root, object? previousFocus)
    {
        if (previousFocus is Control previous && previous.IsLoaded && ReferenceEquals(previous.XamlRoot, root) &&
            previous.Focus(FocusState.Programmatic))
            return;

        var fallback = _focusFallback?.Invoke();
        if (fallback is { IsLoaded: true } && ReferenceEquals(fallback.XamlRoot, root))
            fallback.Focus(FocusState.Programmatic);
    }

    private static DialogResult ToDialogResult(ContentDialogResult result, bool isConfirmation) => result switch
    {
        ContentDialogResult.Primary when isConfirmation => DialogResult.Confirmed,
        ContentDialogResult.Secondary => DialogResult.Cancelled,
        ContentDialogResult.None when isConfirmation => DialogResult.Cancelled,
        _ => DialogResult.Dismissed
    };

    private static void TryHide(ContentDialog dialog)
    {
        try { dialog.Hide(); }
        catch (InvalidOperationException) { }
    }
}
