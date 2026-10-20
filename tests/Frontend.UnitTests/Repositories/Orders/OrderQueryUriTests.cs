using TechStore.Client.Models.Orders;
using TechStore.Client.Repositories.Orders;
using TechStore.Shared.Enums;

namespace TechStore.Client.UnitTests.Repositories.Orders;

public sealed class OrderQueryUriTests
{
    [Fact]
    public void Build_DefaultsUseNewestFirstFirstPageTwentyRows()
    {
        // Act
        var uri = OrderQueryUri.Build(new OrderQuery());

        // Assert
        Assert.Equal("api/orders?sort=createdAt%3Adesc&page=1&pageSize=20", uri);
    }

    [Fact]
    public void Build_IncludesStatusKeywordSortAndPaging()
    {
        // Arrange
        var query = new OrderQuery
        {
            Status = OrderStatus.PartiallyRefunded,
            Query = "  ORD-202610  ",
            Sort = "totalAmount:asc",
            Page = 3,
            PageSize = 50
        };

        // Act
        var uri = OrderQueryUri.Build(query);

        // Assert
        Assert.Equal("api/orders?q=ORD-202610&status=PartiallyRefunded&sort=totalAmount%3Aasc&page=3&pageSize=50", uri);
    }

    [Fact]
    public void Build_EncodesUnicodeAndReservedCharactersInTheKeyword()
    {
        // Arrange
        var query = new OrderQuery { Query = "Nguyễn a&b+c%d#e f" };

        // Act
        var uri = OrderQueryUri.Build(query);

        // Assert
        Assert.Contains("q=Nguy%E1%BB%85n%20a%26b%2Bc%25d%23e%20f", uri);
        Assert.DoesNotContain("#", uri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_OmitsABlankKeyword(string? keyword)
    {
        // Act
        var uri = OrderQueryUri.Build(new OrderQuery { Query = keyword });

        // Assert
        Assert.DoesNotContain("q=", uri);
    }

    [Theory]
    [InlineData("name:asc")]
    [InlineData("price")]
    [InlineData("createdAt:sideways")]
    [InlineData("")]
    public void Build_RejectsASortThatTheApiDoesNotKnow(string sort)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => OrderQueryUri.Build(new OrderQuery { Sort = sort }));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Build_RejectsPagingOutsideTheApiRange(int page, int pageSize)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderQueryUri.Build(new OrderQuery { Page = page, PageSize = pageSize }));
    }

    [Fact]
    public void Build_RejectsAnUndefinedStatus()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => OrderQueryUri.Build(new OrderQuery { Status = (OrderStatus)99 }));
    }
}
