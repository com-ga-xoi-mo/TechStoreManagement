using FluentAssertions;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class StoreSettingsTests
{
    private (FakeStoreSettingRepository Repository, FakeUnitOfWork UnitOfWork, StoreSettingsService Service) CreateSut()
    {
        var repository = new FakeStoreSettingRepository();
        var unitOfWork = new FakeUnitOfWork();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var service = new StoreSettingsService(repository, unitOfWork, timeProvider);

        return (repository, unitOfWork, service);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenEmpty_ReturnsDefaultStoreSettings()
    {
        // Arrange
        var (_, _, service) = CreateSut();

        // Act
        var result = await service.GetSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.StoreName.Should().Contain("TechStore");
        result.Phone.Should().NotBeNullOrWhiteSpace();
        result.Address.Should().NotBeNullOrWhiteSpace();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ValidRequest_PersistsAndReturnsUpdatedSettings()
    {
        // Arrange
        var (repository, unitOfWork, service) = CreateSut();
        var updateRequest = new UpdateStoreSettingsRequest
        {
            StoreName = "TechStore Flagship HCM",
            Address = "99 Lê Lợi, Quận 1, TP.HCM",
            Phone = "0909123456",
            Email = "contact@techstore.vn",
            TaxId = "0312345678",
            Description = "Trung tâm trải nghiệm công nghệ cao cấp"
        };

        // Act
        var result = await service.UpdateSettingsAsync(updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.StoreName.Should().Be("TechStore Flagship HCM");
        result.Address.Should().Be("99 Lê Lợi, Quận 1, TP.HCM");
        result.Phone.Should().Be("0909123456");
        unitOfWork.SaveChangesCount.Should().Be(1);

        var current = await repository.GetCurrentSettingsAsync();
        current.Should().NotBeNull();
        current!.StoreName.Should().Be("TechStore Flagship HCM");
    }

    [Theory]
    [InlineData("", "123 Street", "0909123456")]
    [InlineData("Valid Name", "", "0909123456")]
    [InlineData("Valid Name", "123 Street", "")]
    public async Task UpdateSettingsAsync_MissingRequiredFields_ThrowsArgumentException(string storeName, string address, string phone)
    {
        // Arrange
        var (_, _, service) = CreateSut();
        var updateRequest = new UpdateStoreSettingsRequest
        {
            StoreName = storeName,
            Address = address,
            Phone = phone
        };

        // Act
        var act = () => service.UpdateSettingsAsync(updateRequest);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
