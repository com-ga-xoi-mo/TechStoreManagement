using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Warranty;
using TechStore.Client.Repositories.Warranty;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Warranty;

public sealed partial class WarrantyCaseDetailDialogViewModel(IWarrantyRepository repository, ISessionStore session) : PageViewModel
{
    public Guid CaseId { get; set; }
    public bool HasChanges { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(InspectAllowed), nameof(RepairAllowed), nameof(ReplacementAllowed),
        nameof(ReturnAllowed), nameof(ShowNote), nameof(Summary), nameof(HasCost), nameof(CostLabel), nameof(IsRejection), nameof(ActionForm))]
    private WarrantyDetails? details;
    private static readonly InspectionChoice RepairChoice = new("Repair", "Sửa máy");
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsRejection))]
    private InspectionChoice? selectedOutcome = RepairChoice;
    [ObservableProperty] private string note = string.Empty;
    [ObservableProperty] private string rejectionReason = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool showReplacements;
    [ObservableProperty] private IReadOnlyList<WarrantyReplacement> replacements = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanReplace))] private WarrantyReplacement? selectedReplacement;
    [ObservableProperty] private string replacementMessage = string.Empty;
    [ObservableProperty] private string operationMessage = string.Empty;

    public sealed record InspectionChoice(string Value, string Label);
    public IReadOnlyList<InspectionChoice> Outcomes { get; } =
        [RepairChoice, new("Replace", "Đề nghị đổi máy"), new("Reject", "Từ chối bảo hành")];
    private WarrantyCaseActions Actions => WarrantyCaseActions.For(Details?.Row.Status, session.Current.Role);
    public bool InspectAllowed => Actions.Inspect;
    public bool RepairAllowed => Actions.Repaired;
    public bool ReplacementAllowed => Actions.DecideReplacement;
    public bool ReturnAllowed => Actions.Return;
    public bool ShowNote => InspectAllowed || RepairAllowed || ReplacementAllowed;
    public enum FormKind { None, Inspection, Repair, Replacement, Return }
    public FormKind ActionForm => InspectAllowed ? FormKind.Inspection
        : RepairAllowed ? FormKind.Repair
        : ReplacementAllowed ? FormKind.Replacement
        : ReturnAllowed ? FormKind.Return : FormKind.None;
    public bool CanReplace => SelectedReplacement != null;
    public bool IsRejection => InspectAllowed && SelectedOutcome?.Value == "Reject";
    public bool HasCost => Details?.ReplacementCost != null;
    public string CostLabel => Details?.ReplacementCost is { } cost ? $"Giá vốn máy thay: {OrderPresentation.FormatMoney(cost)}" : "";
    public string Summary => Details is not { } item ? "" :
        $"{item.Row.Code} · {item.Row.StatusLabel}\n{item.Row.Product} {item.VariantName} · {item.Row.Device ?? "—"}\n" +
        $"Đơn gốc: {item.OrderCode ?? "—"} · {WarrantyLabels.Date(item.PurchasedAt)}\n" +
        $"Bảo hành: {WarrantyLabels.Date(item.WarrantyStartAt)} đến {WarrantyLabels.Date(item.WarrantyEndAt)}\n" +
        $"Liên hệ: {item.ContactName ?? "—"} · {item.ContactPhone ?? "—"}\n" +
        $"Lỗi khách báo: {item.Issue ?? "—"}\nKiểm định: {item.Inspection ?? "—"}" +
        (item.Rejection == null ? "" : $"\nLý do từ chối: {item.Rejection}") +
        (item.Replacement == null ? "" : $"\nMáy thay: {item.Replacement}") +
        (HasCost ? $"\n{CostLabel}" : "") +
        (item.Events.Count == 0 ? "" : "\n\nLịch sử\n" + string.Join("\n", item.Events.Select(x => x.Label)));

    protected override async Task<bool> LoadContentAsync(CancellationToken token)
    {
        var result = await repository.GetAsync(CaseId, token);
        token.ThrowIfCancellationRequested();
        Details = result;
        return true;
    }

    protected override string GetSafeErrorMessage(Exception exception) =>
        exception is WarrantyRequestException ? exception.Message : base.GetSafeErrorMessage(exception);

    [RelayCommand]
    private Task InspectAsync() => ChangeAsync(InspectAllowed, token => repository.InspectAsync(CaseId,
        new(SelectedOutcome?.Value, Note, RejectionReason), token));

    [RelayCommand]
    private Task RepairedAsync() => ChangeAsync(RepairAllowed, token => repository.RepairedAsync(CaseId, Note, token));

    [RelayCommand]
    private Task RejectReplacementAsync() =>
        ChangeAsync(ReplacementAllowed, token => repository.RejectReplacementAsync(CaseId, Note, token));

    [RelayCommand]
    private Task ReplaceAsync() => SelectedReplacement is { } selected
        ? ChangeAsync(ReplacementAllowed, token => repository.ReplaceAsync(CaseId, selected.Id, token))
        : Task.CompletedTask;

    [RelayCommand]
    private Task ReturnAsync() => ChangeAsync(ReturnAllowed, token => repository.ReturnAsync(CaseId, token));

    [RelayCommand]
    private Task FindReplacementsAsync() => RunTrackedRequestAsync(async token =>
    {
        if (IsBusy || !ReplacementAllowed || Details is not { } details) return;
        IsBusy = true;
        OperationMessage = "";
        ResetReplacements();
        ShowReplacements = true;
        try
        {
            var result = await repository.ReplacementsAsync(details.ProductId, details.VariantId, token);
            token.ThrowIfCancellationRequested();
            Replacements = result;
            ReplacementMessage = result.Count == 0 ? "Không còn máy cùng loại trong kho" : "";
        }
        catch (WarrantyRequestException error) when (!token.IsCancellationRequested) { OperationMessage = error.Message; }
        finally { IsBusy = false; }
    });

    private Task ChangeAsync(bool allowed, Func<CancellationToken, Task<WarrantyDetails>> action) => RunTrackedRequestAsync(async token =>
    {
        if (IsBusy || !allowed) return;
        IsBusy = true;
        OperationMessage = "";
        try
        {
            var result = await action(token);
            token.ThrowIfCancellationRequested();
            Details = result;
            HasChanges = true;
            Note = "";
            RejectionReason = "";
            ResetReplacements();
        }
        catch (WarrantyRequestException error) when (!token.IsCancellationRequested)
        {
            OperationMessage = error.Message;
            if (error.StatusCode == 409)
            {
                ResetReplacements();
                try
                {
                    var current = await repository.GetAsync(CaseId, token);
                    token.ThrowIfCancellationRequested();
                    HasChanges |= current.Row.Status != Details?.Row.Status;
                    Details = current;
                }
                catch (WarrantyRequestException reload) when (!token.IsCancellationRequested)
                {
                    Details = null;
                    OperationMessage += $"\n{reload.Message}";
                    ContentState = PageContentState.Error;
                }
            }
        }
        finally { IsBusy = false; }
    });

    private void ResetReplacements()
    {
        ShowReplacements = false;
        SelectedReplacement = null;
        Replacements = [];
        ReplacementMessage = "";
    }
}
