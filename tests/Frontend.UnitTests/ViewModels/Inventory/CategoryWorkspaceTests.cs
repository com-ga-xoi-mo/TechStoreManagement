using FluentAssertions;
using TechStore.Client.Controls;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Pages;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Frontend.UnitTests.Features.Categories;

public class CategoryWorkspaceTests
{
    private sealed class FakeApi(IReadOnlyList<CategoryNode> roots) : ICategoryWorkspaceRepository
    {
        public int Calls { get; private set; }
        public int UpdateCalls { get; private set; }
        public Guid LastUpdatedId { get; private set; }
        public UpdateCategoryRequest? LastUpdateRequest { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<CategoryNode>>>? Handler { get; set; }
        public Func<CreateCategoryRequest, CancellationToken, Task<CategoryDto>>? CreateHandler { get; set; }
        public Func<Guid, UpdateCategoryRequest, CancellationToken, Task<CategoryDto>>? UpdateHandler { get; set; }
        public Func<Guid, CancellationToken, Task>? DeleteHandler { get; set; }
        public Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Handler?.Invoke(cancellationToken) ?? Task.FromResult(roots);
        }

        public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            return CreateHandler?.Invoke(request, cancellationToken) ?? Task.FromResult(new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name ?? "",
                Slug = request.Slug ?? "",
                IsActive = request.IsActive ?? true
            });
        }

        public Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastUpdatedId = id;
            LastUpdateRequest = request;
            return UpdateHandler?.Invoke(id, request, cancellationToken) ?? Task.FromResult(new CategoryDto
            {
                Id = id,
                Name = request.Name ?? string.Empty,
                Slug = request.Slug ?? string.Empty,
                ParentId = request.ParentId,
                IsActive = request.IsActive ?? true
            });
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            DeleteHandler?.Invoke(id, cancellationToken) ?? Task.CompletedTask;
    }

    private sealed class FakeDialogs : ICategoryDialogRequests
    {
        public List<CategoryDialogRequest> Requests { get; } = [];
        public void Request(CategoryDialogRequest request) => Requests.Add(request);
    }

    private sealed class StockApi : IInventoryRepository
    {
        public int Calls { get; private set; }
        public Func<Task<PagedResultDto<InventoryStockDto>>>? StockHandler { get; set; }
        public Func<Task<PagedResultDto<SerialImeiDto>>>? SerialHandler { get; set; }

        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (StockHandler is not null) return StockHandler();
            return Task.FromResult(new PagedResultDto<InventoryStockDto>
            {
                Items = [new InventoryStockDto { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "Phone", ProductSku = "P-1", Quantity = 8 }],
                Page = request.Page ?? 1, PageSize = request.PageSize ?? 20, TotalCount = 1, TotalPages = 1
            });
        }

        public Task<PagedResultDto<SerialImeiDto>> GetSerialsAsync(SerialImeiFilterRequest request, CancellationToken cancellationToken = default)
        {
            if (SerialHandler is not null) return SerialHandler();
            return Task.FromResult(new PagedResultDto<SerialImeiDto>
            {
                Items = [], Page = 1, PageSize = 20, TotalCount = 0, TotalPages = 0
            });
        }
    }

    private static CategoryManagementViewModel CreateVm(
        ICategoryWorkspaceRepository api,
        ICategoryDialogRequests? dialogs = null,
        InventoryStockViewModel? inventory = null,
        SerialImeiViewModel? serials = null)
    {
        var stockApi = new StockApi();
        return new CategoryManagementViewModel(
            api,
            dialogs ?? new FakeDialogs(),
            inventory ?? new InventoryStockViewModel(stockApi),
            serials ?? new SerialImeiViewModel(stockApi));
    }

    private static CategoryNode Node(string name, bool active = true, params CategoryNode[] children) =>
        new(Guid.NewGuid(), name, name.ToLowerInvariant().Replace(' ', '-'), active, children);

    private static async Task<CategoryManagementViewModel> Load(params CategoryNode[] roots)
    {
        var vm = CreateVm(new FakeApi(roots));
        await vm.ActivateAsync();
        return vm;
    }

    [Fact]
    public async Task Load_NestedTree_PreservesHierarchyAndIdentity()
    {
        // Arrange
        var leaf = Node("Deep child");
        var child = Node("Child", true, leaf);
        var root = Node("Root", true, child);
        var vm = await Load(root, Node("Other"));
        // Act
        var rows = vm.VisibleRows.ToArray();
        // Assert
        rows.Select(row => row.Name).Should().Equal("Root", "Child", "Deep child", "Other");
        rows.Select(row => row.Depth).Should().Equal(0, 1, 2, 0);
        rows[2].ParentName.Should().Be("Child");
        rows[0].ParentName.Should().Be("Danh mục gốc");
        rows[0].IsExpanded.Should().BeTrue();
        vm.TotalCount.Should().Be(4);
        await vm.ActivateAsync();
        vm.VisibleRows.Should().HaveCount(4);
    }

    [Fact]
    public async Task Search_DeepMatch_OpensAncestorsAndRestoresExpansion()
    {
        // Arrange
        var api = new FakeApi([Node("Root", true, Node("Branch", true, Node("Needle")), Node("Unrelated"))]);
        var vm = CreateVm(api);
        await vm.ActivateAsync();
        var root = vm.VisibleRows[0];
        vm.ToggleExpansionCommand.Execute(root);
        // Act
        vm.SearchText = "  nEeDlE  ";
        // Assert
        vm.VisibleRows.Select(row => row.Name).Should().Equal("Root", "Branch", "Needle");
        root.IsForcedOpen.Should().BeTrue();
        root.CanToggle.Should().BeFalse();
        vm.ToggleExpansionCommand.Execute(root);
        vm.VisibleRows.Should().HaveCount(3);
        vm.FilteredCount.Should().Be(1);
        vm.EligibleCount.Should().Be(1);
        vm.VisibleRows.Count(row => row.IsContextOnly).Should().Be(2);
        vm.SearchText = "";
        vm.VisibleRows.Should().ContainSingle().Which.Should().BeSameAs(root);
        root.IsExpanded.Should().BeFalse();
        api.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(true, false, CategoryStatusFilter.Hidden)]
    [InlineData(false, true, CategoryStatusFilter.Active)]
    public async Task Filter_ChildStatus_PreservesOppositeStatusAncestor(bool parentActive, bool childActive, CategoryStatusFilter filter)
    {
        // Arrange
        var vm = await Load(Node("Parent", parentActive, Node("Target", childActive), Node("Sibling", parentActive)));
        // Act
        vm.StatusFilter = filter;
        vm.SearchText = "target";
        // Assert
        vm.VisibleRows.Select(row => row.Name).Should().Equal("Parent", "Target");
        vm.VisibleRows[0].IsActive.Should().Be(parentActive);
        vm.VisibleRows[0].IsContextOnly.Should().BeTrue();
        vm.EligibleCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_ParentMatch_DoesNotIncludeNonmatchingChildren()
    {
        // Arrange
        var vm = await Load(Node("Parent", true, Node("Child")));
        // Act
        vm.SearchText = "Parent";
        // Assert
        vm.VisibleRows.Should().ContainSingle().Which.Name.Should().Be("Parent");
        vm.SearchText = "Absent";
        vm.IsEmpty.Should().BeTrue();
        vm.FooterText.Should().Be("Hiển thị 0 - 0 trên tổng số 0 danh mục");
    }

    [Fact]
    public async Task Expansion_IndependentBranches_PreservesNestedStateAndRows()
    {
        // Arrange
        var vm = await Load(Node("A", true, Node("B", true, Node("C"))), Node("D", true, Node("E")));
        var a = vm.VisibleRows[0];
        var b = vm.VisibleRows[1];
        var d = vm.VisibleRows[3];
        // Act
        vm.ToggleExpansionCommand.Execute(b);
        vm.ToggleExpansionCommand.Execute(a);
        vm.ToggleExpansionCommand.Execute(a);
        // Assert
        vm.VisibleRows.Select(row => row.Name).Should().Equal("A", "B", "D", "E");
        b.IsExpanded.Should().BeFalse();
        vm.VisibleRows[2].Should().BeSameAs(d);
        vm.VisibleRows[^1].CanToggle.Should().BeFalse();
        vm.TotalCount.Should().Be(5);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(15, 1, 15)]
    [InlineData(16, 2, 15)]
    public async Task Pagination_Boundaries_ReportsAccurateRanges(int count, int pages, int firstPageRows)
    {
        // Arrange
        var vm = await Load(Enumerable.Range(0, count).Select(i => Node($"Row {i}")).ToArray());
        // Act / Assert
        vm.PageCount.Should().Be(pages);
        vm.VisibleRows.Should().HaveCount(firstPageRows);
        vm.CanGoPrevious.Should().BeFalse();
        vm.CanGoNext.Should().Be(pages > 1);
        vm.FooterText.Should().Be($"Hiển thị {(count == 0 ? 0 : 1)} - {firstPageRows} trên tổng số {count} danh mục");
        vm.IsEmpty.Should().Be(count == 0);
    }

    [Fact]
    public async Task Pagination_LargeBranch_RepeatsContextWithoutCountingIt()
    {
        // Arrange
        var vm = await Load(Node("Root", true, Enumerable.Range(1, 17).Select(i => Node($"Child {i}")).ToArray()));
        var root = vm.VisibleRows[0];
        // Act
        vm.NextPageCommand.Execute(null);
        // Assert
        vm.CurrentPage.Should().Be(2);
        vm.VisibleRows.Select(row => row.Name).Should().Equal("Root", "Child 15", "Child 16", "Child 17");
        vm.VisibleRows[0].IsContextOnly.Should().BeTrue();
        vm.FooterText.Should().Be("Hiển thị 16 - 18 trên tổng số 18 danh mục");
        vm.CanGoNext.Should().BeFalse();
        vm.ToggleExpansionCommand.Execute(root);
        vm.CurrentPage.Should().Be(1);
        vm.EligibleCount.Should().Be(1);
        vm.TotalCount.Should().Be(18);
    }

    [Fact]
    public async Task Filtering_OnLaterPage_ResetsPageAndDeduplicatesContext()
    {
        // Arrange
        var vm = await Load(Node("Root", true, Enumerable.Range(1, 20).Select(i => Node($"Child {i}", false)).ToArray()));
        vm.NextPageCommand.Execute(null);
        // Act
        vm.StatusFilter = CategoryStatusFilter.Hidden;
        // Assert
        vm.CurrentPage.Should().Be(1);
        vm.VisibleRows.Should().HaveCount(16);
        vm.VisibleRows.Count(row => row.IsContextOnly).Should().Be(1);
        vm.EligibleCount.Should().Be(20);
        vm.NextPageCommand.Execute(null);
        vm.VisibleRows.Should().HaveCount(6);
        vm.FooterText.Should().Be("Hiển thị 16 - 20 trên tổng số 20 danh mục");
    }

    [Theory]
    [InlineData("Điện thoại thông minh", "", "phone", StatusTone.Info)]
    [InlineData("", "LAPTOP-MACBOOK", "laptop", StatusTone.Accent)]
    [InlineData("iPad Apple", "", "tablet", StatusTone.Success)]
    [InlineData("Tai nghe & Loa Bluetooth", "", "headphones", StatusTone.Warning)]
    [InlineData("Headphones", "", "headphones", StatusTone.Warning)]
    [InlineData("Phụ kiện", "", "headphones", StatusTone.Warning)]
    [InlineData("Apple", "", "category", StatusTone.Neutral)]
    [InlineData("Samsung", "", "category", StatusTone.Neutral)]
    [InlineData("Unknown", "", "category", StatusTone.Neutral)]
    [InlineData(null, null, "category", StatusTone.Neutral)]
    public void IconResolver_NamesAndSlugs_SelectsSpecificFamily(string? name, string? slug, string symbol, StatusTone expectedTone)
    {
        // Act
        var icon = CategoryIconResolver.Resolve(name, slug);
        // Assert
        icon.Symbol.Should().Be(symbol);
        icon.Tone.Should().Be(expectedTone);
    }

    [Fact]
    public async Task Counts_MissingOrZero_AreDistinctAndNotCategoryTotals()
    {
        // Arrange
        var child = Node("Child") with { SkuCount = 0 };
        var root = Node("Root", true, child) with { ProductCount = 42 };
        var vm = await Load(root, Node("Unknown"));
        // Assert
        vm.VisibleRows.Select(row => row.CountText).Should().Equal("42 sản phẩm", "0 SKU", "—");
        vm.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Actions_SelectedRow_EmitsOneRequestAndDoesNotMutateTree()
    {
        // Arrange
        var api = new FakeApi([Node("Parent", true, Node("Selected"))]);
        var dialogs = new FakeDialogs();
        var vm = CreateVm(api, dialogs);
        await vm.ActivateAsync();
        var selected = vm.VisibleRows[1];
        // Act
        vm.AddCategoryCommand.Execute(null);
        vm.EditCategoryCommand.Execute(selected);
        vm.DeleteCategoryCommand.Execute(selected);
        // Assert
        dialogs.Requests.Should().Equal(new CategoryDialogRequest(CategoryDialogAction.Add, null),
            new(CategoryDialogAction.Edit, selected.Id), new(CategoryDialogAction.Delete, selected.Id));
        vm.VisibleRows.Should().HaveCount(2);
        api.Calls.Should().Be(1);
    }

    [Fact]
    public async Task WorkspaceTabs_SelectInventoryAndReturnToCategoriesWithoutReloadingTree()
    {
        var api = new FakeApi([Node("Root")]);
        var vm = CreateVm(api);
        await vm.ActivateAsync();
        var calls = api.Calls;

        vm.SelectTabCommand.Execute(CategoryWorkspaceTab.Inventory);
        Assert.True(vm.IsInventoryTab);
        Assert.False(vm.IsCategoryTab);
        vm.SelectTabCommand.Execute(CategoryWorkspaceTab.Categories);

        Assert.True(vm.IsCategoryTab);
        Assert.False(vm.IsInventoryTab);
        Assert.Equal(calls, api.Calls);
    }

    [Fact]
    public async Task WorkspaceTabs_RefreshInventoryOnceAndReturnWithoutStaleCategoryRows()
    {
        var categoryApi = new FakeApi([Node("Category")]);
        var inventoryApi = new StockApi();
        var inventoryVm = new InventoryStockViewModel(inventoryApi);
        var categoryVm = CreateVm(categoryApi, inventory: inventoryVm);
        await categoryVm.ActivateAsync();

        categoryVm.SelectTabCommand.Execute(CategoryWorkspaceTab.Inventory);
        await categoryVm.Inventory.LoadCommand.ExecuteAsync(null);
        categoryVm.SelectTabCommand.Execute(CategoryWorkspaceTab.Categories);

        Assert.True(categoryVm.IsCategoryTab);
        Assert.Single(categoryVm.VisibleRows);
        Assert.Equal(1, categoryApi.Calls);
        Assert.Equal(1, inventoryApi.Calls);
        Assert.Single(inventoryVm.VisibleItems);
    }

    [Fact]
    public async Task ToggleStatus_SendsOnlyEditableCategoryFields_ThenReloads()
    {
        var id = Guid.NewGuid();
        var active = new CategoryNode(id, "Accessories", "accessories", true, []);
        var api = new FakeApi([active]);
        api.Handler = _ => Task.FromResult<IReadOnlyList<CategoryNode>>(
            api.Calls == 1 ? [active] : [active with { IsActive = false }]);
        var vm = CreateVm(api);
        await vm.ActivateAsync();

        await vm.ToggleStatusCommand.ExecuteAsync(vm.VisibleRows[0]);

        api.UpdateCalls.Should().Be(1);
        api.LastUpdatedId.Should().Be(id);
        api.LastUpdateRequest!.Name.Should().Be("Accessories");
        api.LastUpdateRequest.Slug.Should().Be("accessories");
        api.LastUpdateRequest.IsActive.Should().BeFalse();
        api.Calls.Should().Be(2);
        vm.VisibleRows[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Loading_FailureThenRetry_ClearsBusyAndRecovers()
    {
        // Arrange
        var api = new FakeApi([Node("Success")]) { Handler = _ => throw new CategoryApiException(CategoryApiFailure.Forbidden) };
        var vm = CreateVm(api);
        // Act
        await vm.ActivateAsync();
        // Assert
        vm.ErrorMessage.Should().Be("Bạn không có quyền xem danh mục.");
        vm.IsLoading.Should().BeFalse();
        vm.IsEmpty.Should().BeFalse();
        api.Handler = null;
        await vm.RetryCommand.ExecuteAsync(null);
        vm.IsError.Should().BeFalse();
        vm.VisibleRows.Should().ContainSingle();
    }

    [Fact]
    public async Task ActivateAsync_TreeLoads_ContentState()
    {
        // Arrange
        var vm = CreateVm(new FakeApi([Node("Laptops")]));

        // Act
        await vm.ActivateAsync();

        // Assert
        vm.ContentState.Should().Be(PageContentState.Content);
        vm.ShowStatePanel.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateAsync_Unauthorized_ErrorWithSessionMessage()
    {
        // Arrange
        var api = new FakeApi([]) { Handler = _ => throw new CategoryApiException(CategoryApiFailure.Unauthorized) };
        var vm = CreateVm(api);

        // Act
        await vm.ActivateAsync();

        // Assert
        vm.ContentState.Should().Be(PageContentState.Error);
        vm.IsError.Should().BeTrue();
        vm.ShowStatePanel.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
    }

    [Fact]
    public async Task Filter_NoMatch_EmptyThenBackToContent()
    {
        // Arrange
        var vm = await Load(Node("Laptops"));
        vm.ContentState.Should().Be(PageContentState.Content);

        // Act 1: Filter to no match
        vm.SearchText = "NoMatch";

        // Assert 1
        vm.ContentState.Should().Be(PageContentState.Empty);
        vm.IsEmpty.Should().BeTrue();
        vm.ShowStatePanel.Should().BeTrue();

        // Act 2: Clear filter
        vm.SearchText = "";

        // Assert 2
        vm.ContentState.Should().Be(PageContentState.Content);
        vm.IsEmpty.Should().BeFalse();
        vm.ShowStatePanel.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_CancelsTabViewModels()
    {
        // Arrange
        var pendingStocks = new TaskCompletionSource<PagedResultDto<InventoryStockDto>>();
        var pendingSerials = new TaskCompletionSource<PagedResultDto<SerialImeiDto>>();
        var stockApi = new StockApi
        {
            StockHandler = () => pendingStocks.Task,
            SerialHandler = () => pendingSerials.Task
        };
        var inventoryVm = new InventoryStockViewModel(stockApi);
        var serialsVm = new SerialImeiViewModel(stockApi);
        var vm = CreateVm(new FakeApi([Node("Root")]), inventory: inventoryVm, serials: serialsVm);
        await vm.ActivateAsync();

        var stockLoad = inventoryVm.LoadCommand.ExecuteAsync(null);
        var serialLoad = serialsVm.LoadCommand.ExecuteAsync(null);

        // Act
        vm.Deactivate();
        pendingStocks.SetResult(new PagedResultDto<InventoryStockDto>
        {
            Items = [new InventoryStockDto { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), ProductName = "Ignored", ProductSku = "IG" }],
            Page = 1, PageSize = 20, TotalCount = 1, TotalPages = 1
        });
        pendingSerials.SetResult(new PagedResultDto<SerialImeiDto>
        {
            Items = [new SerialImeiDto { Id = Guid.NewGuid(), SerialNumber = "IG" }],
            Page = 1, PageSize = 20, TotalCount = 1, TotalPages = 1
        });
        await Task.WhenAll(stockLoad, serialLoad);

        // Assert
        inventoryVm.VisibleItems.Should().BeEmpty();
        serialsVm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task PageButtons_MarkCurrentPage()
    {
        // Arrange
        var vm = await Load(Enumerable.Range(1, 20).Select(i => Node($"Cat {i}")).ToArray());

        // Act
        vm.GoToPageCommand.Execute(2);

        // Assert
        vm.PageButtons.Should().HaveCount(2);
        vm.PageButtons[0].IsCurrent.Should().BeFalse();
        vm.PageButtons[1].IsCurrent.Should().BeTrue();
    }
}
