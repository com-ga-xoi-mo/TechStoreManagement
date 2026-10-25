using FluentAssertions;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Features.Categories.ViewModels;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Frontend.UnitTests.Features.Categories;

public class CreateCategoryDialogTests
{
    private sealed class MockCategoryWorkspaceRepository : ICategoryWorkspaceRepository
    {
        public CreateCategoryRequest? LastCreateRequest { get; private set; }
        public Func<CreateCategoryRequest, CancellationToken, Task<CategoryDto>>? CreateHandler { get; set; }
        public Func<Guid, UpdateCategoryRequest, CancellationToken, Task<CategoryDto>>? UpdateHandler { get; set; }
        public Func<Guid, CancellationToken, Task>? DeleteHandler { get; set; }

        public Task<IReadOnlyList<CategoryNode>> GetNodesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CategoryNode>>([]);

        public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            if (CreateHandler is not null)
            {
                return CreateHandler(request, cancellationToken);
            }

            return Task.FromResult(new CategoryDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name ?? "",
                Slug = request.Slug ?? "",
                Description = request.Description,
                ParentId = request.ParentId,
                IsActive = request.IsActive ?? true
            });
        }

        public Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default) =>
            UpdateHandler?.Invoke(id, request, cancellationToken) ?? Task.FromResult(new CategoryDto
            {
                Id = id,
                Name = request.Name ?? string.Empty,
                Slug = request.Slug ?? string.Empty,
                Description = request.Description,
                ParentId = request.ParentId,
                IsActive = request.IsActive ?? true
            });

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            DeleteHandler?.Invoke(id, cancellationToken) ?? Task.CompletedTask;
    }

    [Theory]
    [InlineData("Tai nghe & Âm thanh", "tai-nghe-am-thanh")]
    [InlineData("Đồng hồ thông minh", "dong-ho-thong-minh")]
    [InlineData("Điện thoại & Tablet cao cấp!", "dien-thoai-tablet-cao-cap")]
    [InlineData("  Laptop / PC Gaming  ", "laptop-pc-gaming")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void GenerateSlug_ProducesNormalizedUrlSafeSlugs(string input, string expected)
    {
        CreateCategoryDialogViewModel.GenerateSlug(input).Should().Be(expected);
    }

    [Fact]
    public void InitialState_And_ParentOptions_CorrectlyConfigured()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository();
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var hierarchy = new List<CategoryNode>
        {
            new(rootId, "Điện thoại", "dien-thoai", true, [
                new(childId, "iPhone", "iphone", true, [])
            ])
        };

        // Act
        var vm = new CreateCategoryDialogViewModel(api, hierarchy);

        // Assert
        vm.IsActive.Should().BeTrue();
        vm.KeepCreating.Should().BeFalse();
        vm.CanSave.Should().BeFalse();
        vm.ParentOptions.Should().HaveCount(3);
        vm.ParentOptions[0].DisplayName.Should().Be("-- Danh mục gốc (Cấp 1) --");
        vm.ParentOptions[0].Id.Should().BeNull();
        vm.ParentOptions[1].DisplayName.Should().Be("Điện thoại");
        vm.ParentOptions[1].Id.Should().Be(rootId);
        vm.ParentOptions[2].DisplayName.Should().Contain("iPhone");
        vm.ParentOptions[2].Id.Should().Be(childId);
        vm.SelectedParentOption.Should().Be(vm.ParentOptions[0]);
    }

    [Fact]
    public void NameChange_UpdatesAutoSlug_And_CanSave()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository();
        var vm = new CreateCategoryDialogViewModel(api);

        // Act
        vm.Name = "Tai nghe & Âm thanh";

        // Assert
        vm.AutoSlug.Should().Be("tai-nghe-am-thanh");
        vm.CanSave.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_WithoutKeepCreating_CallsApi_FiresEvents_And_RequestsClose()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository();
        var vm = new CreateCategoryDialogViewModel(api)
        {
            Name = "Tai nghe Bluetooth",
            Description = "Tai nghe không dây",
            KeepCreating = false
        };

        CategoryDto? createdResult = null;
        var closeRequested = false;
        vm.CategoryCreated += (_, dto) => createdResult = dto;
        vm.RequestClose += (_, _) => closeRequested = true;

        // Act
        await vm.SaveAsync();

        // Assert
        api.LastCreateRequest.Should().NotBeNull();
        api.LastCreateRequest!.Name.Should().Be("Tai nghe Bluetooth");
        api.LastCreateRequest.Slug.Should().Be("tai-nghe-bluetooth");
        api.LastCreateRequest.Description.Should().Be("Tai nghe không dây");
        api.LastCreateRequest.ParentId.Should().BeNull();
        api.LastCreateRequest.IsActive.Should().BeTrue();

        createdResult.Should().NotBeNull();
        createdResult!.Name.Should().Be("Tai nghe Bluetooth");
        closeRequested.Should().BeTrue();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_WithKeepCreating_ResetsDraft_And_KeepsDialogOpen()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository();
        var vm = new CreateCategoryDialogViewModel(api)
        {
            Name = "Tai nghe Bluetooth",
            Description = "Mô tả",
            KeepCreating = true
        };

        CategoryDto? createdResult = null;
        var closeRequested = false;
        vm.CategoryCreated += (_, dto) => createdResult = dto;
        vm.RequestClose += (_, _) => closeRequested = true;

        // Act
        await vm.SaveAsync();

        // Assert
        createdResult.Should().NotBeNull();
        closeRequested.Should().BeFalse(); // Dialog stays open!
        vm.Name.Should().BeEmpty();
        vm.AutoSlug.Should().BeEmpty();
        vm.Description.Should().BeEmpty();
        vm.CanSave.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_OnConflict409_PreservesDraft_And_ShowsErrorMessage()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository
        {
            CreateHandler = (_, _) => throw new CategoryApiException(CategoryApiFailure.Conflict, "Slug 'tai-nghe' đã tồn tại.")
        };
        var vm = new CreateCategoryDialogViewModel(api)
        {
            Name = "Tai nghe",
            Description = "Mô tả không mất",
            KeepCreating = false
        };

        var closeRequested = false;
        vm.RequestClose += (_, _) => closeRequested = true;

        // Act
        await vm.SaveAsync();

        // Assert
        closeRequested.Should().BeFalse();
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Slug 'tai-nghe' đã tồn tại.");
        // Draft data MUST be preserved!
        vm.Name.Should().Be("Tai nghe");
        vm.AutoSlug.Should().Be("tai-nghe");
        vm.Description.Should().Be("Mô tả không mất");
    }

    [Fact]
    public void CycleIcon_ChangesSelectedIcon()
    {
        // Arrange
        var api = new MockCategoryWorkspaceRepository();
        var vm = new CreateCategoryDialogViewModel(api);
        var initial = vm.SelectedIcon;

        // Act
        vm.CycleIconCommand.Execute(null);

        // Assert
        vm.SelectedIcon.Should().NotBe(initial);
    }

    [Fact]
    public void IconOptions_ExposeDistinctLocalIconsAndLabels()
    {
        // Arrange
        var vm = new CreateCategoryDialogViewModel(new MockCategoryWorkspaceRepository());

        // Act
        vm.SelectIconCommand.Execute("videocam");

        // Assert
        vm.IconOptions.Should().Contain("phone");
        vm.IconOptions.Should().Contain("router");
        vm.IconOptions.Should().OnlyHaveUniqueItems();
        vm.SelectedIcon.Should().Be("videocam");
        vm.SelectedIconLabel.Should().Be("Video camera");
    }

    [Fact]
    public void ResetDraft_RestoresDefaultIcon()
    {
        // Arrange
        var vm = new CreateCategoryDialogViewModel(new MockCategoryWorkspaceRepository())
        {
            SelectedIcon = "camera"
        };

        // Act
        vm.ResetDraft();

        // Assert
        vm.SelectedIcon.Should().Be("folder");
        vm.SelectedIconLabel.Should().Be("Danh mục");
    }
}
