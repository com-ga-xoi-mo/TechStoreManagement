using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Orders;

public sealed partial class ReturnInspectionsViewModel(IReturnRepository returns, ISessionStore session)
    : ReturnWorkspaceViewModel(session)
{
    private (Guid LineId, InspectReturnRequest Request)? _unconfirmed;
    [ObservableProperty] private IReadOnlyList<InspectionBatch> batches = [];
    [ObservableProperty] private InspectionBatch? selected;
    [ObservableProperty] private string quantity = "1";
    public bool CanChange => _unconfirmed is null;
    public bool CanInspect => IsContent && Selected is { Pending: > 0 } && CanChange;
    public bool CanEditQuantity => CanInspect && Selected?.Serial is null;
    public bool NeedsRetry => !CanChange;
    public string Result => Selected?.Result ?? "";
    protected override bool HasLoadedContent => Batches.Count > 0;
    protected override void OnContentStateChanged()
    {
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(CanInspect));
        OnPropertyChanged(nameof(CanEditQuantity));
    }
    public string EmptyMessage => IsEmpty ? "Chưa có lô hàng trả." : "";

    partial void OnSelectedChanged(InspectionBatch? value)
    {
        Quantity = "1";
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(CanEditQuantity));
        OnPropertyChanged(nameof(CanInspect));
    }

    protected override async Task LoadAsync(CancellationToken token)
    {
        var id = Selected?.LineId;
        var rows = await returns.InspectionsAsync(token);
        token.ThrowIfCancellationRequested();
        Batches = rows;
        Selected = rows.FirstOrDefault(x => x.LineId == id) ?? rows.FirstOrDefault(x => x.Pending > 0) ?? rows.FirstOrDefault();
    }

    [RelayCommand] private Task Refresh() => ReloadAsync();

    [RelayCommand]
    private Task Inspect(string? outcome) => ExecuteAsync(async token =>
    {
        if (!RoleAccess.CanManageStock(CurrentRole)) return;
        var pending = _unconfirmed;
        if (pending is null)
        {
            if (!CanInspect || outcome is not ("InStock" or "Defective" or "UnderRepair") || Selected is not { Pending: > 0 } batch) return;
            var count = 1;
            if (batch.Serial is null && (!int.TryParse(Quantity, out count) || count < 1 || count > batch.Pending))
            {
                Message = $"Số lượng phải từ 1 đến {batch.Pending}.";
                return;
            }
            pending = (batch.LineId, new(Guid.NewGuid(), count, outcome));
        }

        InspectionBatch result;
        try { result = await returns.InspectAsync(pending.Value.LineId, pending.Value.Request, token); }
        catch (ApiException ex) when (!token.IsCancellationRequested)
        {
            SetPending(IsUncertain(ex) ? pending : null);
            if (!NeedsRetry) throw;
            Message = $"Chưa xác nhận kết quả. Thử lại mã kiểm định {pending.Value.Request.OperationId:D}.";
            return;
        }
        token.ThrowIfCancellationRequested();
        SetPending(null);
        Batches = Batches.Select(b => b.LineId == result.LineId ? result : b).ToArray();
        Selected = result;
        Message = $"Đã ghi kiểm định. Lượng còn chờ {result.Pending}.";
    });

    private void SetPending((Guid, InspectReturnRequest)? pending)
    {
        _unconfirmed = pending;
        OnPropertyChanged(nameof(CanChange));
        OnPropertyChanged(nameof(CanEditQuantity));
        OnPropertyChanged(nameof(CanInspect));
        OnPropertyChanged(nameof(NeedsRetry));
    }

    protected override void ClearPage()
    {
        SetPending(null);
        Batches = [];
        Selected = null;
    }
}
