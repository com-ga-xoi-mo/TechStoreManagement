using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class CategoryServiceTests
{
    private readonly FakeCategoryRepository _categoryRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_categoryRepository, _unitOfWork);
    }

    [Fact]
    public async Task GetTreeAsync_Should_Build_Hierarchical_Tree_Of_Active_Categories()
    {
        // Arrange
        var root1 = new Category { Id = Guid.NewGuid(), Name = "Laptops", Slug = "laptops", IsActive = true };
        var root2 = new Category { Id = Guid.NewGuid(), Name = "Smartphones", Slug = "smartphones", IsActive = true };
        var sub1 = new Category { Id = Guid.NewGuid(), Name = "Gaming Laptops", Slug = "gaming-laptops", ParentId = root1.Id, IsActive = true };
        var sub2 = new Category { Id = Guid.NewGuid(), Name = "Ultrabooks", Slug = "ultrabooks", ParentId = root1.Id, IsActive = true };
        var subSub = new Category { Id = Guid.NewGuid(), Name = "ROG Series", Slug = "rog-series", ParentId = sub1.Id, IsActive = true };

        _categoryRepository.Add(root1);
        _categoryRepository.Add(root2);
        _categoryRepository.Add(sub1);
        _categoryRepository.Add(sub2);
        _categoryRepository.Add(subSub);

        // Act
        var result = await _service.GetTreeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(2);

        var laptopNode = result.Data.First(c => c.Id == root1.Id);
        laptopNode.SubCategories.Should().HaveCount(2);

        var gamingNode = laptopNode.SubCategories.First(c => c.Id == sub1.Id);
        gamingNode.SubCategories.Should().HaveCount(1);
        gamingNode.SubCategories[0].Id.Should().Be(subSub.Id);
    }

    [Fact]
    public async Task GetTreeAsync_Should_Exclude_Inactive_Categories()
    {
        // Arrange
        var activeRoot = new Category { Id = Guid.NewGuid(), Name = "Active", Slug = "active", IsActive = true };
        var inactiveRoot = new Category { Id = Guid.NewGuid(), Name = "Inactive", Slug = "inactive", IsActive = false };

        _categoryRepository.Add(activeRoot);
        _categoryRepository.Add(inactiveRoot);

        // Act
        var result = await _service.GetTreeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data[0].Id.Should().Be(activeRoot.Id);
    }

    [Fact]
    public async Task CreateAsync_Should_Succeed_With_Valid_Top_Level_Category()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Tablets",
            Description = "All tablets"
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Tablets");
        result.Data.Slug.Should().Be("tablets");
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_Succeed_With_Valid_SubCategory()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        _categoryRepository.Add(new Category { Id = parentId, Name = "Audio", Slug = "audio", IsActive = true });

        var request = new CreateCategoryRequest
        {
            Name = "Headphones",
            Slug = "headphones",
            ParentId = parentId
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ParentId.Should().Be(parentId);
        _unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Should_Fail_When_Name_Is_Missing()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "   "
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("name");
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_Should_Fail_When_ParentId_Does_Not_Exist()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Accessories",
            ParentId = Guid.NewGuid()
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("parentId");
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_Conflict_When_Slug_Already_Exists()
    {
        // Arrange
        _categoryRepository.Add(new Category { Id = Guid.NewGuid(), Name = "Smartphones", Slug = "smartphones", IsActive = true });

        var request = new CreateCategoryRequest
        {
            Name = "Smartphones New",
            Slug = "smartphones"
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("smartphones");
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_Should_Fail_When_Slug_Format_Is_Invalid()
    {
        // Arrange
        var request = new CreateCategoryRequest
        {
            Name = "Category",
            Slug = "Invalid_Slug!"
        };

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().ContainKey("slug");
        _unitOfWork.SaveChangesCount.Should().Be(0);
    }
}
