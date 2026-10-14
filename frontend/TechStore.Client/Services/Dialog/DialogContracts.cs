namespace TechStore.Client.Services.Dialog;

public enum DialogResult
{
    Confirmed,
    Cancelled,
    Dismissed
}

public sealed record DialogRequest(
    string Title,
    string Message,
    string PrimaryButtonText,
    string? SecondaryButtonText = null);

public interface IDialogService
{
    Task<DialogResult> ConfirmAsync(
        DialogRequest request,
        CancellationToken cancellationToken = default);

    Task<DialogResult> InformAsync(
        DialogRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDialogPresenter
{
    bool IsReady { get; }
    Task<DialogResult> PresentAsync(DialogRequest request, bool isConfirmation, CancellationToken cancellationToken);
}

public sealed class DialogUnavailableException(string message) : InvalidOperationException(message);
