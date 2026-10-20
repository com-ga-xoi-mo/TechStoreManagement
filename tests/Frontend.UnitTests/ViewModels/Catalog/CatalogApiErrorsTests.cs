using TechStore.Client.Services.Api;
using TechStore.Client.ViewModels.Catalog;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Catalog;

public sealed class CatalogApiErrorsTests
{
    private const string Subject = "Sản phẩm";

    [Fact]
    public void Describe_Timeout_ReturnsTimeoutMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Timeout, "Timeout");

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Yêu cầu quá thời gian chờ. Hãy tải lại sản phẩm để kiểm tra trước khi thử lại.", result);
    }

    [Fact]
    public void Describe_Network_ReturnsNetworkMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Network, "Network error");

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Không thể kết nối API. Hãy kiểm tra kết nối rồi thử lại.", result);
    }

    [Fact]
    public void Describe_InvalidResponse_ReturnsInvalidResponseMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.InvalidResponse, "Invalid JSON");

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("API trả dữ liệu không đúng định dạng.", result);
    }

    [Fact]
    public void Describe_Status404_ReturnsNotFoundWithSubject()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Not Found", statusCode: 404);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Sản phẩm không còn tồn tại.", result);
    }

    [Fact]
    public void Describe_Status409_WithDetail_ReturnsServerDetail()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Conflict", statusCode: 409, detail: "Sản phẩm đang có đơn hàng.");

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Sản phẩm đang có đơn hàng.", result);
    }

    [Fact]
    public void Describe_Status409_WithoutDetail_ReturnsDefaultConflictMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Conflict", statusCode: 409);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Sản phẩm không thể thực hiện lúc này.", result);
    }

    [Fact]
    public void Describe_Status400_WithFieldError_ReturnsFirstFieldError()
    {
        // Arrange
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["name"] = new[] { "Tên sản phẩm không được để trống." }
        };
        var ex = new ApiException(ApiErrorKind.Http, "Bad Request", statusCode: 400, fieldErrors: fieldErrors);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Tên sản phẩm không được để trống.", result);
    }

    [Fact]
    public void Describe_Status401_ReturnsUnauthorizedMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Unauthorized", statusCode: 401);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Phiên truy cập đã hết hạn. Hãy đăng nhập lại để tiếp tục.", result);
    }

    [Fact]
    public void Describe_Status403_ReturnsForbiddenMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Forbidden", statusCode: 403);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", result);
    }

    [Fact]
    public void Describe_Status500_ReturnsGenericErrorMessage()
    {
        // Arrange
        var ex = new ApiException(ApiErrorKind.Http, "Internal Server Error", statusCode: 500);

        // Act
        var result = CatalogApiErrors.Describe(ex, Subject);

        // Assert
        Assert.Equal("Không thể hoàn tất thao tác. Hãy thử lại.", result);
    }
}
