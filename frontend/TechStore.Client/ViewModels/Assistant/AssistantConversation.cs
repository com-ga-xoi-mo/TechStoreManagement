using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TechStore.Client.Models.Assistant;
using TechStore.Client.Repositories.Assistant;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;

namespace TechStore.Client.ViewModels.Assistant;

/// <summary>
/// In-memory chat shared by every AssistantPage instance: switching Shell tabs keeps the messages and a pending
/// request keeps running. Tied to the session generation: a new sign-in or sign-out cancels and clears it;
/// token renewal keeps it.
/// </summary>
public sealed partial class AssistantConversation : ObservableObject
{
    private long? _generation;
    private CancellationTokenSource? _pending;
    private string? _failedPrompt;

    [ObservableProperty] private bool isSending;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;

    public AssistantConversation(ISessionStore? session = null)
    {
        // Only cancels (thread-safe); the collections are cleared on the UI thread by EnsureSession.
        if (session is not null) session.SessionChanged += (_, _) => CancelPending();
    }

    public ObservableCollection<AssistantTurn> Messages { get; } = [];
    public List<AssistantTurn> History { get; } = [];
    public bool CanRetry => _failedPrompt is not null && !IsSending;

    /// <summary>Clears the chat when <paramref name="generation"/> differs; returns true when it was cleared.</summary>
    public bool EnsureSession(long generation)
    {
        if (_generation == generation) return false;
        _generation = generation;
        CancelPending();
        Messages.Clear();
        History.Clear();
        _failedPrompt = null;
        IsSending = false;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        return true;
    }

    public void Cancel() => CancelPending();

    public void Clear(string greeting)
    {
        if (IsSending) return;
        History.Clear();
        Messages.Clear();
        Messages.Add(new AssistantTurn(false, greeting));
        _failedPrompt = null;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }

    public Task RetryAsync(IAssistantRepository repository) =>
        _failedPrompt is { } prompt ? SendAsync(repository, prompt) : Task.CompletedTask;

    public async Task SendAsync(IAssistantRepository repository, string prompt)
    {
        if (IsSending) return;
        var generation = _generation;
        _failedPrompt = null;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsSending = true;
        var userTurn = new AssistantTurn(true, prompt);
        Messages.Add(userTurn);
        var request = History.TakeLast(19).SkipWhile(turn => !turn.IsUser).Append(userTurn).ToArray();
        using var cancellation = new CancellationTokenSource();
        _pending = cancellation;
        try
        {
            var answer = await repository.SendAsync(request, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            History.Add(userTurn);
            Messages.Add(new AssistantTurn(false, answer.Reply));
            if (!string.IsNullOrWhiteSpace(answer.Reply))
                History.Add(new AssistantTurn(false, answer.Reply.Trim()[..Math.Min(answer.Reply.Trim().Length, 2000)]));
            if (History.Count > 20) History.RemoveRange(0, History.Count - 20);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (generation != _generation) return;
            Messages.Remove(userTurn);
            StatusMessage = "Đã hủy";
        }
        catch (Exception exception)
        {
            if (generation != _generation) return;
            Messages.Remove(userTurn);
            _failedPrompt = prompt;
            ErrorMessage = exception switch
            {
                ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Vui lòng thử lại.",
                ApiException { Kind: ApiErrorKind.Timeout } => "Trợ lý phản hồi quá lâu. Vui lòng thử lại.",
                ApiException api => api.Detail ?? api.Title ?? "Không thể kết nối trợ lý. Vui lòng thử lại.",
                _ => "Không thể kết nối trợ lý. Vui lòng thử lại."
            };
        }
        finally
        {
            if (ReferenceEquals(_pending, cancellation)) _pending = null;
            if (generation == _generation) IsSending = false;
            OnPropertyChanged(nameof(CanRetry));
        }
    }

    private void CancelPending()
    {
        try { _pending?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}
