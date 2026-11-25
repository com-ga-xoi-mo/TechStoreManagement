using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TechStore.Client.Models.Analytics;
using TechStore.Client.Repositories.Analytics;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Navigation;
using TechStore.Client.Services.Session;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared;

namespace TechStore.Client.ViewModels.Dashboard;

public sealed partial class FinancialReportViewModel(
    IFinancialReportRepository repository,
    ISessionStore sessions,
    TimeProvider clock,
    bool live,
    Func<Func<CancellationToken, Task>, Task<bool>> trackRequest) : ObservableObject
{
    private const string InputFormat = "dd/MM/yyyy";
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private long? _activationGeneration;
    private CancellationToken _activationToken;
    private SynchronizationContext? _activationContext;
    private CancellationTokenSource? _request;
    private long _sequence;
    private FinancialReportRange? _appliedRange;

    [ObservableProperty] private string from = string.Empty;
    [ObservableProperty] private string to = string.Empty;
    [ObservableProperty] private string fromError = string.Empty;
    [ObservableProperty] private string toError = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOverview), nameof(IsOverviewTabSelected), nameof(IsFinancialTabSelected))]
    private bool isOpen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(IsEmpty), nameof(IsError), nameof(ReloadLabel))]
    private PageContentState state;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSnapshot), nameof(Presentation))]
    private FinancialReportSnapshot? snapshot;

    public bool IsAvailable
    {
        get
        {
            var session = sessions.Current;
            return live && !_activationToken.IsCancellationRequested && _activationGeneration == session.Generation
                && session.Status == SessionStatus.Active
                && RoleAccess.CanOpen(AppRoute.Root(ShellDestination.Dashboard), session.Role);
        }
    }

    public bool ShowOverview => !IsOpen;

    public bool? IsOverviewTabSelected
    {
        get => ShowOverview;
        set
        {
            if (value == true && IsOpen) Close();
        }
    }

    public bool? IsFinancialTabSelected
    {
        get => IsOpen;
        set
        {
            if (value == true && !IsOpen)
            {
                if (IsAvailable)
                    _ = OpenAsync();
                else if (SynchronizationContext.Current is { } context)
                    context.Post(_ => NotifyTabSelectionChanged(), null);
                else
                    NotifyTabSelectionChanged();
            }
        }
    }

    public bool IsLoading => State == PageContentState.Loading;
    public bool IsEmpty => State == PageContentState.Empty;
    public bool IsError => State == PageContentState.Error;
    public bool HasSnapshot => Snapshot is not null;
    public string ReloadLabel => IsError ? "Thử lại" : "Làm mới";
    public FinancialReportPresentation? Presentation { get; private set; }

    internal void Activate(CancellationToken activationToken)
    {
        _activationToken = activationToken;
        _activationContext = SynchronizationContext.Current;
        _activationGeneration = sessions.Current.Generation;
        sessions.SessionChanged += OnSessionChanged;
        OnPropertyChanged(nameof(IsAvailable));
    }

    internal void Deactivate()
    {
        sessions.SessionChanged -= OnSessionChanged;
        Dispatch(() =>
        {
            _activationGeneration = null;
            ResetSession();
        });
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenAsync()
    {
        if (!IsAvailable || IsOpen) return Task.CompletedTask;

        if (_appliedRange is null)
        {
            var today = Today();
            _appliedRange = new(today.AddDays(-6), today);
        }

        From = _appliedRange.Value.From.ToString(InputFormat, Vietnamese);
        To = _appliedRange.Value.To.ToString(InputFormat, Vietnamese);
        FromError = string.Empty;
        ToError = string.Empty;

        IsOpen = true;
        return LoadAsync(_appliedRange.Value);
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        CancelCurrent();
        Snapshot = null;
        ErrorMessage = string.Empty;
        State = PageContentState.Idle;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ApplyAsync()
    {
        if (!IsOpen || !IsAvailable) return Task.CompletedTask;

        var validFrom = DateOnly.TryParseExact(From, InputFormat, Vietnamese, DateTimeStyles.None, out var start);
        var validTo = DateOnly.TryParseExact(To, InputFormat, Vietnamese, DateTimeStyles.None, out var end);
        FromError = validFrom ? string.Empty : "Nhập ngày bắt đầu theo dd/MM/yyyy.";
        ToError = validTo ? string.Empty : "Nhập ngày kết thúc theo dd/MM/yyyy.";
        if (validFrom && start == DateOnly.MinValue)
            FromError = "Ngày bắt đầu nằm ngoài khoảng hỗ trợ.";
        if (validTo && end > Today())
            ToError = "Chọn ngày kết thúc không muộn hơn hôm nay theo giờ Việt Nam.";
        else if (validFrom && validTo)
        {
            if (end < start) ToError = "Ngày kết thúc phải từ ngày bắt đầu trở đi.";
            else if (end.DayNumber - start.DayNumber >= AnalyticsLimits.MaxCustomRangeDays)
                ToError = $"Chọn khoảng tối đa {AnalyticsLimits.MaxCustomRangeDays} ngày.";
        }

        if (FromError.Length > 0 || ToError.Length > 0) return Task.CompletedTask;

        var range = new FinancialReportRange(start, end);
        _appliedRange = range;
        return LoadAsync(range);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task ReloadAsync() => _appliedRange is { } range ? LoadAsync(range) : Task.CompletedTask;

    partial void OnFromChanged(string value)
    {
        FromError = string.Empty;
        ToError = string.Empty;
    }

    partial void OnToChanged(string value) => ToError = string.Empty;

    partial void OnSnapshotChanged(FinancialReportSnapshot? value) =>
        Presentation = value is null ? null : new FinancialReportPresentation(value);

    private Task LoadAsync(FinancialReportRange range)
    {
        if (!IsOpen || !IsAvailable) return Task.CompletedTask;

        CancelCurrent();
        var sequence = _sequence;
        var generation = _activationGeneration;
        Snapshot = null;
        ErrorMessage = string.Empty;
        State = PageContentState.Loading;

        return trackRequest(async token =>
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token);
            _request = request;
            try
            {
                if (!OwnsRequest() || request.IsCancellationRequested) return;
                var result = await repository.GetFinancialReportAsync(range, request.Token);
                if (!OwnsRequest() || request.IsCancellationRequested) return;
                Snapshot = result;
                State = Presentation!.IsEmpty ? PageContentState.Empty : PageContentState.Content;
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (!OwnsRequest() || request.IsCancellationRequested) return;
                ErrorMessage = SafeError(error);
                State = PageContentState.Error;
            }
            finally
            {
                if (OwnsRequest() && IsLoading) State = PageContentState.Idle;
                if (ReferenceEquals(_request, request)) _request = null;
            }
        });

        bool OwnsRequest() => sequence == _sequence && generation == _activationGeneration
            && _appliedRange == range && IsOpen && IsAvailable;
    }

    private void CancelCurrent()
    {
        ++_sequence;
        var previous = _request;
        _request = null;
        previous?.Cancel();
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(VietnamOffset).DateTime);

    private void OnSessionChanged(object? sender, SessionChangedEventArgs args) => Dispatch(() =>
    {
        if (!IsAvailable) ResetSession();
    });

    private void ResetSession()
    {
        Close();
        _appliedRange = null;
        From = To = FromError = ToError = string.Empty;
        OnPropertyChanged(nameof(IsAvailable));
    }

    private void NotifyTabSelectionChanged()
    {
        OnPropertyChanged(nameof(IsOverviewTabSelected));
        OnPropertyChanged(nameof(IsFinancialTabSelected));
    }

    private void Dispatch(Action action)
    {
        if (_activationContext is { } context && SynchronizationContext.Current != context)
            context.Post(_ => action(), null);
        else
            action();
    }

    private static string SafeError(Exception error) => error switch
    {
        ApiException { StatusCode: 400 } => "Kỳ báo cáo không hợp lệ. Hãy kiểm tra khoảng ngày.",
        ApiException { StatusCode: 401 } => "Phiên truy cập đã hết hạn. Hãy đăng nhập lại.",
        ApiException { StatusCode: 403 } => "Bạn không có quyền xem báo cáo tài chính.",
        ApiException { Kind: ApiErrorKind.Timeout } => "Yêu cầu quá thời gian chờ. Hãy thử lại.",
        ApiException { Kind: ApiErrorKind.Network } => "Không thể kết nối máy chủ. Hãy kiểm tra kết nối rồi thử lại.",
        ApiException { Kind: ApiErrorKind.InvalidResponse } => "Máy chủ trả dữ liệu báo cáo không đúng định dạng.",
        _ => "Chưa tải được báo cáo tài chính. Hãy thử lại."
    };
}
