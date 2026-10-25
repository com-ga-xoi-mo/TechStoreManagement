using FluentAssertions;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Categories.ViewModels;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Frontend.UnitTests.Features.Categories;

public sealed class EditCategoryDialogTests
{
    private sealed class FakeApi : ICategoryWorkspaceRepository
    {
        public UpdateCategoryRequest? LastRequest { get; private set; }
        public Guid? LastId { get; private set; }
        public Func<Guid, UpdateCategoryRequest, CancellationToken, Task<CategoryDto>>? UpdateHandler { get; set; }
        public Func<Guid, CancellationToken, Task>? DeleteHandler { get; set; }

        public Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryNode>>([]);

        public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CategoryDto { Id = Guid.NewGuid(), Name = request.Name ?? string.Empty });

        public Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            LastId = id;
            LastRequest = request;
            return UpdateHandler?.Invoke(id, request, cancellationToken)
                ?? Task.FromResult(new CategoryDto
                {
                    Id = id, Name = request.Name ?? string.Empty, Slug = request.Slug ?? string.Empty,
                    ParentId = request.ParentId, Description = request.Description, IsActive = request.IsActive ?? true
                });
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            DeleteHandler?.Invoke(id, cancellationToken) ?? Task.CompletedTask;
    }

    private static CategoryNode Node(
        string name,
        Guid? id = null,
        bool active = true,
        Guid? parentId = null,
        string? description = null,
        params CategoryNode[] children) =>
        new(id ?? Guid.NewGuid(), name, name.ToLowerInvariant().Replace(' ', '-'), active, children,
            Description: description, ParentId: parentId);

    [Fact]
    public void Constructor_PrefillsDraftAndRootOptionFirst()
    {
        var api = new FakeApi();
        var parent = Node("Điện thoại");
        var child = Node("iPhone", parentId: parent.Id, description: "Thiết bị Apple");
        var roots = new[] { parent with { Children = [child] }, Node("Laptop") };

        var vm = new EditCategoryDialogViewModel(api, child, roots);

        vm.Name.Should().Be("iPhone");
        vm.Description.Should().Be("Thiết bị Apple");
        vm.SelectedParentId.Should().Be(parent.Id);
        vm.IsRoot.Should().BeFalse();
        vm.LevelBadgeText.Should().Be("Danh mục con");
        vm.ParentOptions[0].Id.Should().BeNull();
        vm.ParentOptions[0].DisplayName.Should().Be(EditCategoryDialogViewModel.RootParentLabel);
        vm.ParentOptions.Should().Contain(option => option.Id == parent.Id);
    }

    [Fact]
    public void RootContext_AlwaysShowsChildAndProductCounts()
    {
        var category = Node("Accessories") with { ProductCount = 0 };

        var vm = new EditCategoryDialogViewModel(new FakeApi(), category, [category]);

        vm.ContextText.Should().Be("Đang chứa 0 danh mục con và 0 sản phẩm.");
    }

    [Fact]
    public void ParentOptions_ExcludeSelfAllDescendantsAndDuplicateReferences()
    {
        var api = new FakeApi();
        var targetId = Guid.NewGuid();
        var deepId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();
        var target = Node("Root", targetId, children:
            Node("Child", children: Node("Deep", deepId, children: Node("Hidden", hiddenId, active: false))));
        var other = Node("Other root");

        var vm = new EditCategoryDialogViewModel(api, target, [target, other, target]);
        var ids = vm.ParentOptions.Where(option => option.Id is not null).Select(option => option.Id!.Value).ToArray();

        ids.Should().NotContain(targetId);
        ids.Should().NotContain(deepId);
        ids.Should().NotContain(hiddenId);
        ids.Should().Contain(other.Id);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SaveAsync_SendsExactDraftAndRaisesCloseOnlyAfterSuccess()
    {
        var api = new FakeApi();
        var category = Node("Tai nghe", description: "Cũ") with { Slug = "tai-nghe" };
        var closeCount = 0;
        var updatedCount = 0;
        var vm = new EditCategoryDialogViewModel(api, category, [category])
        {
            Name = "Tai nghe Bluetooth",
            Description = string.Empty,
            IsActive = false,
            SelectedParentOption = null
        };
        vm.RequestClose += (_, _) => closeCount++;
        vm.CategoryUpdated += (_, _) => updatedCount++;

        await vm.SaveCommand.ExecuteAsync(null);

        api.LastId.Should().Be(category.Id);
        api.LastRequest.Should().NotBeNull();
        api.LastRequest!.Name.Should().Be("Tai nghe Bluetooth");
        api.LastRequest.Slug.Should().Be("tai-nghe");
        api.LastRequest.Description.Should().BeEmpty();
        api.LastRequest.ParentId.Should().BeNull();
        api.LastRequest.IsActive.Should().BeFalse();
        updatedCount.Should().Be(1);
        closeCount.Should().Be(1);
        vm.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData(CategoryApiFailure.BadRequest)]
    [InlineData(CategoryApiFailure.NotFound)]
    [InlineData(CategoryApiFailure.Conflict)]
    public async Task SaveAsync_ServerFailure_PreservesDraftAndShowsError(CategoryApiFailure failure)
    {
        var api = new FakeApi
        {
            UpdateHandler = (_, _, _) => throw new CategoryApiException(failure, "Không thể cập nhật.")
        };
        var category = Node("Tai nghe", description: "Giữ lại") with { Slug = "tai-nghe" };
        var vm = new EditCategoryDialogViewModel(api, category, [category])
        {
            Name = "Tên đã chỉnh sửa",
            Description = "Draft chưa mất",
            IsActive = false
        };
        var closeCount = 0;
        vm.RequestClose += (_, _) => closeCount++;

        await vm.SaveCommand.ExecuteAsync(null);

        closeCount.Should().Be(0);
        vm.Name.Should().Be("Tên đã chỉnh sửa");
        vm.Description.Should().Be("Draft chưa mất");
        vm.IsActive.Should().BeFalse();
        vm.ErrorMessage.Should().Be("Không thể cập nhật.");
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void InvalidName_DisablesSaveAndExposesValidationMessage()
    {
        var category = Node("Tai nghe");
        var vm = new EditCategoryDialogViewModel(new FakeApi(), category, [category]);

        vm.Name = new string('x', EditCategoryDialogViewModel.MaxNameLength + 1);

        vm.CanSave.Should().BeFalse();
        vm.ErrorMessage.Should().Contain("200");
    }

    [Fact]
    public void MissingCurrentParent_DoesNotSilentlyBecomeRoot()
    {
        var category = Node("Child", parentId: Guid.NewGuid());
        var vm = new EditCategoryDialogViewModel(new FakeApi(), category, [category]);

        vm.CurrentParentUnavailable.Should().BeTrue();
        vm.SelectedParentId.Should().NotBeNull();
        vm.CanSave.Should().BeFalse();

        vm.SelectedParentOption = vm.ParentOptions[0];

        vm.CurrentParentUnavailable.Should().BeFalse();
        vm.SelectedParentId.Should().BeNull();
        vm.CanSave.Should().BeTrue();
    }
}
