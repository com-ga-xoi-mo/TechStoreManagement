using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class SerialImeiViewModelTests
{
    [Fact]
    public async Task Load_MapsPageDataTotalsAndPageNumbers()
    {
        var api = new FakeApi(Page(page: 2, pageSize: 20, totalCount: 45, Serial("A"), Serial("B")));
        var vm = new SerialImeiViewModel(api) { Page = 2 };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("B", vm.Items[1].SerialNumber);
        Assert.Equal("45 Serial / IMEI", vm.TotalLabel);
        Assert.Equal("Hiển thị 21 – 40 trên tổng số 45 Serial / IMEI", vm.FooterText);
        Assert.Equal([1, 2, 3], vm.PageNumbers);
        Assert.True(vm.HasLoaded);
        Assert.False(vm.HasError);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task FilterChange_TrimsSearchResetsPageAndReloads()
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 0));
        var vm = new SerialImeiViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Page = 2;

        vm.SearchText = "  IP15  ";
        vm.StatusFilter = SerialImeiStatusFilter.Sold;

        Assert.Equal("IP15", api.LastRequest!.Search);
        Assert.Equal(SerialImeiStatus.Sold, api.LastRequest.Status);
        Assert.Equal(1, api.LastRequest.Page);
        Assert.Equal(SerialImeiViewModel.DefaultPageSize, api.LastRequest.PageSize);
        Assert.Equal(3, api.Calls);
    }

    [Fact]
    public async Task Load_SendsNullSearchWhenWhitespace()
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 0));
        var vm = new SerialImeiViewModel(api) { SearchText = "   " };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Null(api.LastRequest!.Search);
    }

    [Theory]
    [InlineData(SerialImeiStatusFilter.All, null)]
    [InlineData(SerialImeiStatusFilter.InStock, SerialImeiStatus.InStock)]
    [InlineData(SerialImeiStatusFilter.Reserved, SerialImeiStatus.Reserved)]
    [InlineData(SerialImeiStatusFilter.Sold, SerialImeiStatus.Sold)]
    [InlineData(SerialImeiStatusFilter.Returned, SerialImeiStatus.Returned)]
    [InlineData(SerialImeiStatusFilter.UnderRepair, SerialImeiStatus.UnderRepair)]
    [InlineData(SerialImeiStatusFilter.Defective, SerialImeiStatus.Defective)]
    public async Task Load_MapsEveryStatusOptionToContract(SerialImeiStatusFilter filter, SerialImeiStatus? expected)
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 0));
        var vm = new SerialImeiViewModel(api) { StatusFilter = filter };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(expected, api.LastRequest!.Status);
    }

    [Fact]
    public async Task Pagination_ClampsToBoundsAndTogglesCommands()
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 45));
        var vm = new SerialImeiViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.CanGoNext);
        Assert.False(vm.CanGoPrevious);
        Assert.True(vm.NextPageCommand.CanExecute(null));
        Assert.False(vm.PreviousPageCommand.CanExecute(null));

        await vm.GoToPageCommand.ExecuteAsync(99);
        Assert.Equal(3, vm.Page);
        Assert.False(vm.CanGoNext);
        Assert.False(vm.NextPageCommand.CanExecute(null));

        await vm.GoToPageCommand.ExecuteAsync(0);
        Assert.Equal(1, vm.Page);
        Assert.True(vm.CanGoNext);

        await vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Page);
        Assert.True(vm.CanGoPrevious);
    }

    [Fact]
    public async Task Load_Should_ShowPermissionMessageAndClearRowsOnForbidden()
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 5, Serial("A")));
        var vm = new SerialImeiViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);

        api.Handler = (_, _) => Task.FromException<PagedResultDto<SerialImeiDto>>(
            new ApiException(ApiErrorKind.Http, "forbidden", 403));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Bạn không có quyền xem Serial / IMEI.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        Assert.False(vm.IsEmpty);
        Assert.Empty(vm.Items);
        Assert.Equal(0, vm.TotalCount);
        Assert.Empty(vm.PageNumbers);
    }

    [Fact]
    public async Task Load_Should_ShowRetryableMessageAndRecoverOnRetry()
    {
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 5, Serial("A")));
        var vm = new SerialImeiViewModel(api);
        await vm.LoadCommand.ExecuteAsync(null);

        api.Handler = (_, _) => Task.FromException<PagedResultDto<SerialImeiDto>>(new InvalidOperationException("boom"));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Không thể tải danh sách Serial / IMEI. Vui lòng thử lại.", vm.ErrorMessage);
        Assert.Empty(vm.Items);
        Assert.Equal(0, vm.TotalPages);

        api.Handler = null;
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Items);
        Assert.Equal(1, vm.TotalPages);
    }

    [Fact]
    public async Task Load_Should_IgnoreStaleResponseAfterNewerLoad()
    {
        var newer = new TaskCompletionSource<PagedResultDto<SerialImeiDto>>();
        var older = new TaskCompletionSource<PagedResultDto<SerialImeiDto>>();
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 0))
        {
            Handler = (_, call) => (call == 1 ? older : newer).Task
        };
        var vm = new SerialImeiViewModel(api);

        var staleLoad = vm.LoadCommand.ExecuteAsync(null);
        var freshLoad = vm.LoadCommand.ExecuteAsync(null);
        newer.SetResult(Page(page: 1, pageSize: 20, totalCount: 1, Serial("NEW")));
        await freshLoad;
        older.SetResult(Page(page: 1, pageSize: 20, totalCount: 1, Serial("OLD")));
        await staleLoad;

        Assert.Equal("NEW", Assert.Single(vm.Items).SerialNumber);
        Assert.Equal(1, vm.TotalCount);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task CancelLoading_DuringLoad_DoesNotApplyResult()
    {
        var pending = new TaskCompletionSource<PagedResultDto<SerialImeiDto>>();
        var api = new FakeApi(Page(page: 1, pageSize: 20, totalCount: 0))
        {
            Handler = (_, _) => pending.Task
        };
        var vm = new SerialImeiViewModel(api);

        var loading = vm.LoadCommand.ExecuteAsync(null);
        vm.CancelLoading();
        pending.SetResult(Page(page: 1, pageSize: 20, totalCount: 1, Serial("IGNORED")));
        await loading;

        Assert.Empty(vm.Items);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task PageButtons_MarkCurrentPage()
    {
        var api = new FakeApi(Page(page: 2, pageSize: 20, totalCount: 45, Serial("A")));
        var vm = new SerialImeiViewModel(api) { Page = 2 };

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.PageButtons.Count);
        Assert.False(vm.PageButtons[0].IsCurrent);
        Assert.True(vm.PageButtons[1].IsCurrent);
        Assert.False(vm.PageButtons[2].IsCurrent);
    }

    private static SerialImeiDto Serial(string serialNumber) => new()
    {
        Id = Guid.NewGuid(),
        SerialNumber = serialNumber,
        Status = SerialImeiStatus.Sold
    };

    private static PagedResultDto<SerialImeiDto> Page(int page, int pageSize, int totalCount, params SerialImeiDto[] items) =>
        new(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));

    private sealed class FakeApi(PagedResultDto<SerialImeiDto> page) : IInventoryRepository
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);
        public SerialImeiFilterRequest? LastRequest { get; private set; }
        public Func<SerialImeiFilterRequest, int, Task<PagedResultDto<SerialImeiDto>>>? Handler { get; set; }

        public Task<PagedResultDto<SerialImeiDto>> GetSerialsAsync(
            SerialImeiFilterRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var call = Interlocked.Increment(ref _calls);
            if (Handler is not null) return Handler(request, call);
            return Task.FromResult(new PagedResultDto<SerialImeiDto>(
                page.Items, request.Page ?? page.Page, request.PageSize ?? page.PageSize, page.TotalCount, page.TotalPages));
        }

        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(
            InventoryStockFilterRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PagedResultDto<InventoryStockDto>>(new NotSupportedException());
    }
}
