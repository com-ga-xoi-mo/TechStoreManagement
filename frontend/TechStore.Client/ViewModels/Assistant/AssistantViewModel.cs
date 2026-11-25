using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Assistant;
using TechStore.Client.Repositories.Assistant;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Assistant;

/// <summary>Page view of the shared <see cref="AssistantConversation"/>; sending lives there so it survives tab switches.</summary>
public sealed partial class AssistantViewModel : PageViewModel
{
    private readonly IAssistantRepository _repository;
    private readonly AssistantConversation _conversation;
    private readonly string _greeting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string draft = string.Empty;

    public AssistantViewModel(IAssistantRepository repository, ISessionStore session, AssistantConversation conversation)
    {
        _repository = repository;
        _conversation = conversation;
        _greeting = session.Current.Role switch
        {
            "Admin" or "Manager" => "Xin chào! Tôi có thể giúp tìm sản phẩm, kiểm tra tồn kho và báo cáo doanh thu.",
            "WarehouseStaff" => "Xin chào! Tôi có thể giúp tìm sản phẩm và kiểm tra tồn kho.",
            _ => "Xin chào! Tôi có thể giúp tư vấn sản phẩm và kiểm tra tồn kho."
        };
        conversation.EnsureSession(session.Current.Generation);
        if (Messages.Count == 0) Messages.Add(new AssistantTurn(false, _greeting));
        conversation.PropertyChanged += OnConversationChanged;
        SyncError();
    }

    public ObservableCollection<AssistantTurn> Messages => _conversation.Messages;
    public bool IsSending => _conversation.IsSending;
    public string StatusMessage => _conversation.StatusMessage;

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(Draft) && Draft.Trim().Length <= 2000;
    private bool CanCancel() => IsSending;
    private bool CanClear() => !IsSending;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync()
    {
        var prompt = Draft.Trim();
        Draft = string.Empty;
        return _conversation.SendAsync(_repository, prompt);
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _conversation.Cancel();

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        _conversation.Clear(_greeting);
        Draft = string.Empty;
    }

    protected override Task RetryContentAsync() => _conversation.RetryAsync(_repository);

    protected override void OnDeactivated() => _conversation.PropertyChanged -= OnConversationChanged;

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AssistantConversation.IsSending):
                OnPropertyChanged(nameof(IsSending));
                SendCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                ClearCommand.NotifyCanExecuteChanged();
                break;
            case nameof(AssistantConversation.StatusMessage):
                OnPropertyChanged(nameof(StatusMessage));
                break;
            case nameof(AssistantConversation.ErrorMessage):
                SyncError();
                break;
        }
    }

    private void SyncError()
    {
        ErrorMessage = _conversation.ErrorMessage;
        ContentState = string.IsNullOrEmpty(ErrorMessage) ? PageContentState.Content : PageContentState.Error;
    }
}
