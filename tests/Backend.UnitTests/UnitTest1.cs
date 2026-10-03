using FluentAssertions;
using TechStore.Api.Common.Entities;
using Xunit;

namespace TechStore.UnitTests;

public class SampleEntity : BaseEntity
{
}

public class BaseEntityTests
{
    [Fact]
    public void BaseEntity_Should_Initialize_With_Valid_Guid_And_CreatedAt()
    {
        // Act
        var entity = new SampleEntity();

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entity.UpdatedAt.Should().BeNull();
    }
}
