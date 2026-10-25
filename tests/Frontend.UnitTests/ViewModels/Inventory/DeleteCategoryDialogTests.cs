using FluentAssertions;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Services.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Frontend.UnitTests.Features.Categories;

public sealed class DeleteCategoryDialogTests
{
    private sealed class FakeApi : ICategoryWorkspaceRepository
    {
        public int DeleteCalls { get; private set; }
        public Guid? DeletedId { get; private set; }
        public Func<Guid, CancellationToken, Task>? DeleteHandler { get; set; }

        public Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryNode>>([]);

        public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CategoryDto { Id = Guid.NewGuid(), Name = request.Name ?? string.Empty });

        public Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CategoryDto { Id = id, Name = request.Name ?? string.Empty });

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            DeletedId = id;
            return DeleteHandler?.Invoke(id, cancellationToken) ?? Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_CopiesTargetContextAndDerivedPresentation()
    {
        var api = new FakeApi();
        var id = Guid.NewGuid();
        var category = new CategoryNode(id, "Điện thoại", "smartphones", true,
            [new CategoryNode(Guid.NewGuid(), "iPhone", "iphone", true, [])], ProductCount: 642);

        var vm = new DeleteCategoryDialogViewModel(api, category);

        vm.CategoryId.Should().Be(id);
        vm.CategoryName.Should().Be("Điện thoại");
        vm.LevelName.Should().Be("Danh mục gốc");
        vm.Icon.Symbol.Should().Be("phone");
        vm.ChildCount.Should().Be(1);
        vm.ProductCount.Should().Be(642);
        vm.ChildConstraintText.Should().Contain("1");
        vm.ProductConstraintText.Should().Contain("642");
    }

    [Fact]
    public void Constructor_ChildContext_UsesParentName()
    {
        var vm = new DeleteCategoryDialogViewModel(
            new FakeApi(),
            new CategoryNode(Guid.NewGuid(), "Gaming mouse", "gaming-mouse", true, []),
            "Mouse");

        vm.IsRoot.Should().BeFalse();
        vm.LevelName.Should().Be("Danh mục con trực thuộc Mouse");
    }

    [Fact]
    public void Cancel_RaisesCloseWithoutCallingDelete()
    {
        var api = new FakeApi();
        var vm = new DeleteCategoryDialogViewModel(api, Category("Empty"));
        var closeCalls = 0;
        vm.RequestClose += (_, _) => closeCalls++;

        vm.CancelCommand.Execute(null);

        closeCalls.Should().Be(1);
        api.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmDelete_Success_RaisesDeletedAndCloseOnce()
    {
        var api = new FakeApi();
        var vm = new DeleteCategoryDialogViewModel(api, Category("Empty"));
        var deleted = new List<Guid>();
        var closeCalls = 0;
        vm.CategoryDeleted += (_, id) => deleted.Add(id);
        vm.RequestClose += (_, _) => closeCalls++;

        await vm.ConfirmDeleteAsync();

        api.DeleteCalls.Should().Be(1);
        deleted.Should().ContainSingle().Which.Should().Be(vm.CategoryId);
        closeCalls.Should().Be(1);
        vm.IsBusy.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmDelete_WhileBusy_DoesNotSubmitAgain()
    {
        var api = new FakeApi();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DeleteHandler = (_, _) =>
        {
            started.TrySetResult();
            return pending.Task;
        };
        var vm = new DeleteCategoryDialogViewModel(api, Category("Empty"));

        var first = vm.ConfirmDeleteAsync();
        await started.Task;
        var second = vm.ConfirmDeleteAsync();
        pending.SetResult();
        await Task.WhenAll(first, second);

        api.DeleteCalls.Should().Be(1);
    }

    [Fact]
    public async Task Coordinator_SuccessfulDialog_RefreshesExactlyOnce()
    {
        var api = new FakeApi();
        var vm = new DeleteCategoryDialogViewModel(api, Category("Empty"));
        var coordinator = new CategoryDeleteCoordinator();
        var refreshCalls = 0;

        var result = await coordinator.PresentAsync(
            vm,
            async () =>
            {
                await vm.ConfirmDeleteAsync();
                return true;
            },
            () =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            });

        result.Should().BeTrue();
        refreshCalls.Should().Be(1);
        api.DeleteCalls.Should().Be(1);
    }

    [Fact]
    public async Task Coordinator_CancelledDialog_DoesNotRefresh()
    {
        var api = new FakeApi();
        var vm = new DeleteCategoryDialogViewModel(api, Category("Keep"));
        var coordinator = new CategoryDeleteCoordinator();
        var refreshCalls = 0;

        var result = await coordinator.PresentAsync(
            vm,
            () => Task.FromResult(false),
            () =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            });

        result.Should().BeFalse();
        refreshCalls.Should().Be(0);
        api.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Coordinator_RefreshFailure_DoesNotResubmitDelete()
    {
        var api = new FakeApi();
        var vm = new DeleteCategoryDialogViewModel(api, Category("Empty"));
        var coordinator = new CategoryDeleteCoordinator();

        await FluentActions.Awaiting(() => coordinator.PresentAsync(
                vm,
                async () =>
                {
                    await vm.ConfirmDeleteAsync();
                    return true;
                },
                () => throw new InvalidOperationException("refresh failed")))
            .Should().ThrowAsync<InvalidOperationException>();

        api.DeleteCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(CategoryApiFailure.BadRequest, "danh mục con")]
    [InlineData(CategoryApiFailure.Conflict, "sản phẩm liên kết")]
    public async Task ConfirmDelete_BusinessFailure_PreservesDialogContext(CategoryApiFailure failure, string expectedText)
    {
        var api = new FakeApi
        {
            DeleteHandler = (_, _) => throw new CategoryApiException(failure, "backend detail")
        };
        var category = Category("Keep this category");
        var vm = new DeleteCategoryDialogViewModel(api, category);
        var closeCalls = 0;
        vm.RequestClose += (_, _) => closeCalls++;

        await vm.ConfirmDeleteAsync();

        vm.ErrorMessage.Should().Contain(expectedText);
        vm.CategoryName.Should().Be(category.Name);
        vm.IsBusy.Should().BeFalse();
        closeCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(CategoryApiFailure.Unauthorized, "hết hạn")]
    [InlineData(CategoryApiFailure.Forbidden, "không có quyền")]
    [InlineData(CategoryApiFailure.NotFound, "không còn tồn tại")]
    [InlineData(CategoryApiFailure.Unavailable, "kết nối")]
    public async Task ConfirmDelete_OperationalFailure_ShowsRecoverableMessage(CategoryApiFailure failure, string expectedText)
    {
        var api = new FakeApi
        {
            DeleteHandler = (_, _) => throw new CategoryApiException(failure)
        };
        var vm = new DeleteCategoryDialogViewModel(api, Category("Keep this category"));

        await vm.ConfirmDeleteAsync();

        vm.ErrorMessage.Should().Contain(expectedText);
        vm.IsBusy.Should().BeFalse();
    }

    private static CategoryNode Category(string name) =>
        new(Guid.NewGuid(), name, name.ToLowerInvariant().Replace(' ', '-'), true, []);
}
