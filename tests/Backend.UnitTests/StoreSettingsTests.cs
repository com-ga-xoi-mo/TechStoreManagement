using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Api.Identity.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class StoreSettingsTests
{
    private (AppDbContext DbContext, FakeUnitOfWork UnitOfWork, StoreSettingsService Service) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new AppDbContext(options);
        var unitOfWork = new FakeUnitOfWork(dbContext);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var service = new StoreSettingsService(dbContext, unitOfWork, timeProvider);

        return (dbContext, unitOfWork, service);
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
        var (dbContext, unitOfWork, service) = CreateSut();
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

        var inDb = await dbContext.StoreSettings.FirstOrDefaultAsync();
        inDb.Should().NotBeNull();
        inDb!.StoreName.Should().Be("TechStore Flagship HCM");
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
