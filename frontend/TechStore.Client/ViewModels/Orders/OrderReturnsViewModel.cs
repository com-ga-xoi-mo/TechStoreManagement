using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Controls.Presentation;
using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Shared.Requests;

namespace TechStore.Client.ViewModels.Orders;

public sealed partial class ReturnDraftLine(ReturnBalance balance) : ObservableObject
{
    public ReturnBalance Balance { get; } = balance;
    public string Label => Balance.Label;
    [ObservableProperty] private string quantity = "";
    [ObservableProperty] private string error = "";
}

public sealed partial class OrderReturnsViewModel(IReturnRepository returns, ISessionStore session, INavigationService navigation, IPosRepository shifts)
    : ReturnWorkspaceViewModel(session)
{
    private CreateReturnRequest? _unconfirmedCreate;
    private Guid? _unconfirmedAction;

    public Guid OrderId { get; internal set; }
    public bool CanManage => RoleAccess.CanSettleReturns(CurrentRole);
    public bool IsManager => CurrentRole == "Manager";
    [ObservableProperty] private string openingCash = "";
    public bool CanCheckout => RoleAccess.CanManageOrders(CurrentRole);

    [ObservableProperty] private string orderCode = "";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private IReadOnlyList<ReturnDraftLine> lines = [];
    [ObservableProperty] private IReadOnlyList<ReturnHistory> history = [];
    [ObservableProperty] private ReturnHistory? selected;
    [ObservableProperty] private string bankReference = "";
    [ObservableProperty] private string bankAmount = "";
    [ObservableProperty] private string legacyApprovalTime = "";
    public bool NeedsLegacyApprovalTime => ShowReconciliation && Selected?.BankApprovedAt is null;
    [ObservableProperty] private string bankPaidTime = "";
    [ObservableProperty] private string replacementOrder = "";

    [ObservableProperty] private string reviewReason = "";
    [ObservableProperty] private string legacyHistory = "";
    public bool CanEditDraft => _unconfirmedCreate is null && CanSubmit;
    public bool CanSubmit => _unconfirmedCreate is not null || (ReviewReason.Length == 0 && Lines.Any(line => line.Balance.Remaining > 0));
    public bool ShowApproval => CanManage && Selected?.Status == "Pending";
    public bool ShowReconciliation => CanManage && Selected?.Status == "AwaitingBank";
    public bool ShowExchange => Selected is { Status: "Refunded", ReplacementOrderId: null };
    public bool ShowLink => CanManage && ShowExchange;
    public bool ShowCheckout => CanCheckout && ShowExchange;
    public string Details
    {
        get
        {
            if (Selected is not { } row) return "";
            var parts = new List<string> { $"Phiếu {row.Id:D} · {row.StatusLabel}", row.Reason };
            foreach (var line in row.Lines)
            {
                var balance = Lines.FirstOrDefault(item => item.Balance.ItemId == line.ItemId)?.Balance;
                var product = balance?.Description ?? line.ItemId.ToString();
                parts.Add($"{product} × {line.Quantity} · Tiền hàng {OrderPresentation.FormatMoney(line.Net)} · Thuế {OrderPresentation.FormatMoney(line.Tax)}");
            }
            if (row.BankApprovedAt is { } approval)
                parts.Add($"Đã duyệt chi ngân hàng {approval.ToLocalTime():dd/MM/yyyy HH:mm:ss.ffffff zzz}");
            if (row.RefundedAt is { } at) parts.Add($"Đã hoàn {OrderPresentation.FormatDateTime(at)} · {(row.Method == "Cash" ? "Tiền mặt" : "Ngân hàng")}");
            if (row.BankReference is { } reference) parts.Add($"Chứng từ {reference}");
            if (row.ReplacementOrderId is { } id) parts.Add($"Đơn đổi hàng {id:D}");
            return string.Join("\n", parts);
        }
    }

    partial void OnSelectedChanged(ReturnHistory? value)
    {
        BankReference = BankAmount = BankPaidTime = LegacyApprovalTime = ReplacementOrder = "";
        foreach (var name in new[] { nameof(Details), nameof(ShowApproval), nameof(ShowReconciliation), nameof(NeedsLegacyApprovalTime), nameof(ShowCheckout), nameof(ShowLink) })
            OnPropertyChanged(name);
    }

    protected override Task LoadAsync(CancellationToken token) => LoadOrderAsync(Selected?.Id, token);

    private async Task LoadOrderAsync(Guid? select, CancellationToken token)
    {
        var summary = await returns.OrderAsync(OrderId, token);
        token.ThrowIfCancellationRequested();
        OrderCode = summary.OrderCode;
        Lines = summary.Balances.Select(b => new ReturnDraftLine(b)).ToArray();
        ReviewReason = summary.ReviewReason ?? "";
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(CanEditDraft));
        LegacyHistory = string.Join("\n", (summary.LegacyRefunds ?? []).Select(row =>
        {
            var item = summary.Balances.FirstOrDefault(balance => balance.ItemId == row.ItemId);
            return $"Phiếu cũ {row.Id:D} · {OrderPresentation.FormatDateTime(row.RefundedAt)} · {item?.Description ?? row.ItemId.ToString()} × {row.Quantity}" +
                $" · {OrderPresentation.FormatMoney(row.Amount)} · {(row.Method == "Cash" ? "Tiền mặt" : "Ngân hàng")} · {row.Reason}" +
                $" · {(row.IsRestocked ? "Đã nhập kho" : "Chưa nhập kho")}";
        }));
        History = summary.History.OrderByDescending(h => h.CreatedAt).ToArray();
        Selected = History.FirstOrDefault(h => h.Id == select) ?? History.FirstOrDefault();
        OnPropertyChanged(nameof(Details));
    }

    [RelayCommand] private void Back() => navigation.Navigate(ShellDestination.Orders);
    [RelayCommand] private Task Refresh() => ReloadAsync();

    [RelayCommand]
    private Task Submit() => ExecuteAsync(async token =>
    {
        if (!CanSubmit) return;
        var request = _unconfirmedCreate;
        if (request is not null)
        {
            try
            {
                var found = await returns.GetAsync(request.RequestId, token);
                token.ThrowIfCancellationRequested();
                await FinishCreateAsync(found, token);
                return;
            }
            catch (ApiException ex) when (ex.StatusCode == 404) { }
        }
        else
        {
            var chosen = new List<ReturnLineRequest>();
            var invalid = false;
            foreach (var line in Lines)
            {
                line.Error = "";
                if (string.IsNullOrWhiteSpace(line.Quantity) || line.Quantity.Trim() == "0") continue;
                if (!int.TryParse(line.Quantity, out var count) || count < 1 || count > line.Balance.Remaining)
                {
                    line.Error = $"Số lượng phải từ 1 đến {line.Balance.Remaining}.";
                    invalid = true;
                }
                else chosen.Add(new(line.Balance.ItemId, count, line.Balance.SerialId));
            }
            if (invalid || chosen.Count == 0 || string.IsNullOrWhiteSpace(Reason))
            {
                Message = "Điền lý do và số lượng hợp lệ cho ít nhất một dòng hàng.";
                return;
            }
            request = new(Guid.NewGuid(), OrderId, Reason.Trim(), chosen);
        }
        ReturnHistory result;
        try { result = await returns.CreateAsync(request, token); }
        catch (ApiException ex) when (!token.IsCancellationRequested)
        {
            if (IsUncertain(ex))
            {
                _unconfirmedCreate = request;
                OnPropertyChanged(nameof(CanEditDraft));
                Message = $"Chưa nhận được kết quả. Bấm gửi lại để tra cứu phiếu {request.RequestId:D}.";
                return;
            }
            _unconfirmedCreate = null;
            OnPropertyChanged(nameof(CanEditDraft));
            foreach (var (key, errors) in ex.FieldErrors)
            {
                var match = Regex.Match(key, @"^lines\[(\d+)\]", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var i) && i < request.Lines.Count &&
                    Lines.FirstOrDefault(row => row.Balance.ItemId == request.Lines[i].OrderItemId) is { } line)
                    line.Error = string.Join(" ", errors);
            }
            throw;
        }
        token.ThrowIfCancellationRequested();
        await FinishCreateAsync(result, token);
    });

    private async Task FinishCreateAsync(ReturnHistory result, CancellationToken token)
    {
        _unconfirmedCreate = null;
        OnPropertyChanged(nameof(CanEditDraft));
        Reason = "";
        foreach (var line in Lines) line.Quantity = "";
        await LoadOrderAsync(result.Id, token);
        Message = $"Đã ghi nhận phiếu {result.Id:D}.";
    }

    [RelayCommand]
    private Task OpenRefundShift() => ExecuteAsync(async token =>
    {
        if (!IsManager) return;
        var current = await shifts.GetCurrentShiftAsync(token);
        token.ThrowIfCancellationRequested();
        if (current is { IsOpen: true })
        {
            Message = "Ca tiền mặt của bạn đang mở.";
            return;
        }
        if (!decimal.TryParse(OpeningCash, NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("vi-VN"), out var balance) || balance < 0)
        {
            Message = "Nhập số tiền đầu ca (từ 0 ₫).";
            return;
        }
        await shifts.OpenShiftAsync(balance, token);
        token.ThrowIfCancellationRequested();
        OpeningCash = "";
        Message = "Đã mở ca tiền mặt của bạn.";
    });

    [RelayCommand]
    private Task Settle(string? method) => method is "Cash" or "VietQr" && ShowApproval
        ? RunActionAsync((id, token) => returns.SettleAsync(id, method, token))
        : Task.CompletedTask;

    [RelayCommand]
    private Task Reconcile(string? result)
    {
        if (!ShowReconciliation || result is not ("Paid" or "Failed")) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(BankReference) || !decimal.TryParse(BankAmount, NumberStyles.AllowDecimalPoint,
                CultureInfo.GetCultureInfo("vi-VN"), out var amount) || amount < 0)
        {
            Message = "Nhập mã chứng từ và số tiền thực chi (dùng dấu phẩy cho phần thập phân).";
            return Task.CompletedTask;
        }
        var paid = result == "Paid";
        if ((!paid && amount != 0) || (paid && amount != Selected!.Amount))
        {
            Message = "Số tiền chưa khớp kết quả ngân hàng. Giữ phiếu chờ đối soát.";
            return Task.CompletedTask;
        }
        var paidAt = paid ? ParseTransferTime(BankPaidTime.Trim()) : null;
        if (paid && paidAt is null)
        {
            Message = "Nhập ngày giờ địa phương (24/11/2026 14:35:12) hoặc ISO 8601 có múi giờ.";
            return Task.CompletedTask;
        }
        var legacyApproval = paid && NeedsLegacyApprovalTime ? ParseTransferTime(LegacyApprovalTime.Trim()) : null;
        if (paid && NeedsLegacyApprovalTime && legacyApproval is null)
        {
            Message = "Kiểm tra chứng từ duyệt chi gốc và nhập thời điểm đã xác minh cho phiếu cũ.";
            return Task.CompletedTask;
        }
        var request = new ReconcileReturnRequest(BankReference.Trim(), amount, paid, paidAt, legacyApproval);
        return RunActionAsync((id, token) => returns.ReconcileAsync(id, request, token));
    }

    private static DateTimeOffset? ParseTransferTime(string text)
    {
        string[] localFormats = ["dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss.FFFFFFF", "dd/MM/yyyy HH:mm:ss,FFFFFFF"];
        if (DateTime.TryParseExact(text, localFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            var zone = TimeZoneInfo.Local;
            if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }

        string[] isoFormats = ["yyyy-MM-dd'T'HH:mmzzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
            "yyyy-MM-dd'T'HH:mm'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"];
        return DateTimeOffset.TryParseExact(text, isoFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) ? utc : null;
    }

    [RelayCommand]
    private Task Link()
    {
        if (!ShowLink) return Task.CompletedTask;
        if (!Guid.TryParse(ReplacementOrder, out var id) || id == Guid.Empty)
        {
            Message = "Nhập ID của đơn đổi đã thanh toán.";
            return Task.CompletedTask;
        }
        return RunActionAsync((requestId, token) => returns.LinkAsync(requestId, id, token));
    }

    [RelayCommand]
    private void Exchange()
    {
        if (CanInteract && ShowCheckout && Selected is { } row) navigation.Navigate(AppRoute.PosExchange(row.Id));
    }

    private Task RunActionAsync(Func<Guid, CancellationToken, Task<ReturnHistory>> action) => ExecuteAsync(async token =>
    {
        if (_unconfirmedAction is { } uncertain)
        {
            await CheckActionAsync(uncertain, token);
            return;
        }
        if (Selected is not { } row) return;
        ReturnHistory result;
        try { result = await action(row.Id, token); }
        catch (ApiException ex) when (IsUncertain(ex) && !token.IsCancellationRequested)
        {
            _unconfirmedAction = row.Id;
            await CheckActionAsync(row.Id, token);
            if (ex.StatusCode == 503 && !string.IsNullOrWhiteSpace(ex.Detail)) Message = ex.Detail;
            return;
        }
        token.ThrowIfCancellationRequested();
        UpdateHistory(result);
        await LoadOrderAsync(result.Id, token);
    });

    private async Task CheckActionAsync(Guid id, CancellationToken token)
    {
        var result = await returns.GetAsync(id, token);
        token.ThrowIfCancellationRequested();
        _unconfirmedAction = null;
        UpdateHistory(result);
        await LoadOrderAsync(id, token);
        Message = $"Phiếu {id:D} · {result.StatusLabel}.";
    }

    private void UpdateHistory(ReturnHistory result)
    {
        History = History.Select(row => row.Id == result.Id ? result : row).ToArray();
        Selected = result;
    }

    protected override void ClearPage()
    {
        Lines = [];
        History = [];
        Selected = null;
        OrderCode = Reason = BankReference = BankAmount = BankPaidTime = ReplacementOrder = LegacyHistory = ReviewReason = OpeningCash = "";
        _unconfirmedCreate = null;
        _unconfirmedAction = null;
    }
}
