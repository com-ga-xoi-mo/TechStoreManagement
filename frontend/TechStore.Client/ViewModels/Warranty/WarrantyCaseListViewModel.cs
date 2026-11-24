using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Warranty;
using TechStore.Client.Repositories.Warranty;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;

namespace TechStore.Client.ViewModels.Warranty;

public sealed record WarrantyStatusFilter(string? Value, string Label);

public sealed partial class WarrantyCaseListViewModel(IWarrantyRepository repository, ISessionStore session) : PageViewModel
{
    public IReadOnlyList<WarrantyStatusFilter> Filters { get; } =
        new[] { new WarrantyStatusFilter(null, "Tất cả trạng thái") }.Concat(
            new[] { "Received", "Repairing", "PendingReplacement", "Repaired", "Rejected", "ReturnedToCustomer", "Replaced" }
                .Select(x => new WarrantyStatusFilter(x, WarrantyLabels.Status(x)))).ToArray();

    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private WarrantyStatusFilter selectedFilter = new(null, "Tất cả trạng thái");
    [ObservableProperty] private IReadOnlyList<WarrantyRow> rows = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int currentPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int totalPages;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int totalCount;
    private string? _appliedQuery;
    private string? _appliedStatus;

    public bool CanIntake => RoleAccess.CanReceiveWarranty(session.Current.Role);
    public string PageLabel => $"Trang {CurrentPage}/{Math.Max(1, TotalPages)} · {TotalCount} hồ sơ";

    public event Action? IntakeRequested;
    public event Action<Guid>? CaseDetailRequested;

    [RelayCommand]
    private Task ApplyFilterAsync()
    {
        _appliedQuery = Query;
        _appliedStatus = SelectedFilter?.Value;
        CurrentPage = 1;
        return ReloadAsync();
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        if (IsLoading || CurrentPage <= 1) return;
        CurrentPage--;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (IsLoading || CurrentPage >= TotalPages) return;
        CurrentPage++;
        await ReloadAsync();
    }

    [RelayCommand]
    private void Intake()
    {
        if (CanIntake) IntakeRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenCase(object? parameter)
    {
        if (parameter is WarrantyRow row) CaseDetailRequested?.Invoke(row.Id);
    }

    public Task RefreshAfterIntakeAsync()
    {
        Query = string.Empty;
        SelectedFilter = Filters[0];
        return ApplyFilterAsync();
    }

    public Task RefreshAsync() => ReloadAsync();

    protected override async Task<bool> LoadContentAsync(CancellationToken token)
    {
        Rows = [];
        var result = await repository.ListAsync(_appliedQuery, _appliedStatus, CurrentPage, token);
        token.ThrowIfCancellationRequested();
        Rows = result.Items;
        TotalCount = result.TotalCount;
        TotalPages = result.TotalPages;
        return Rows.Count > 0;
    }

    protected override string GetSafeErrorMessage(Exception exception) =>
        exception is WarrantyRequestException ? exception.Message : base.GetSafeErrorMessage(exception);
}
