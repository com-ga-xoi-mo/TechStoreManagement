using System.Net;
using System.Text;
using TechStore.Client.Repositories.Warranty;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Infrastructure;
using TechStore.Client.UnitTests.Repositories.Orders;

namespace TechStore.Client.UnitTests.ViewModels.Warranty;

public sealed class WarrantyRepositoryTests
{
    [Fact]
    public async Task List_EncodesSearchAndMapsLabels()
    {
        var transport = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"page":2,"totalPages":3,"totalCount":41,"items":[{"caseCode":"BH-202611-0001","status":"Received","productName":"Máy","openedAt":"2026-11-25T00:00:00Z"}]}""",
                Encoding.UTF8, "application/json")
        }));
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://localhost:7207/") };
        var repo = new WarrantyRepository(new ApiClient(http, new() { BaseUrl = http.BaseAddress.ToString() }));
        var result = await repo.ListAsync(" %_ ", "Received", 2, default);
        Assert.EndsWith("api/warranties?page=2&pageSize=20&q=%25_&status=Received", Assert.Single(transport.Requests).RequestUri!.ToString());
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("Chờ kiểm định", Assert.Single(result.Items).StatusLabel);
        Assert.Equal("25/11/2026 07:00", result.Items[0].OpenedLabel);
    }

    [Fact]
    public async Task Replace_MapsProblemDetailsExtensionsFromHttp()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent("""{"status":409,"currentStatus":"Replaced","reasons":["OpenCase"]}""",
                Encoding.UTF8, "application/problem+json")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7207/") };
        var repo = new WarrantyRepository(new ApiClient(http, new() { BaseUrl = http.BaseAddress.ToString() }));

        var error = await Assert.ThrowsAsync<WarrantyRequestException>(() => repo.ReplaceAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("Replaced", error.CurrentStatus);
        Assert.Contains("đang có hồ sơ", error.Message);
    }

    [Fact]
    public async Task Return_PostsToReturnEndpointThroughApiClient()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":"ReturnedToCustomer","events":[]}""", Encoding.UTF8, "application/json")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7207/") };
        var repo = new WarrantyRepository(new ApiClient(http, new() { BaseUrl = http.BaseAddress.ToString() }));
        var id = Guid.NewGuid();

        var returned = await repo.ReturnAsync(id, default);

        Assert.Equal("ReturnedToCustomer", returned.Row.Status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith($"/api/warranties/{id:D}/return", request.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(400, "không hợp lệ")]
    [InlineData(401, "hết hạn")]
    [InlineData(403, "không có quyền")]
    [InlineData(404, "Không tìm thấy")]
    public async Task Get_MapsErrors(int status, string message)
    {
        var repo = new WarrantyRepository(new RecordingApiClient((_, _, _) => throw new ApiException(ApiErrorKind.Http, "test", status)));
        Assert.Contains(message, (await Assert.ThrowsAsync<WarrantyRequestException>(() => repo.GetAsync(Guid.NewGuid(), default))).Message);
    }

    [Fact]
    public async Task Lookup_NetworkFailure_HasSafeMessage()
    {
        var repo = new WarrantyRepository(new RecordingApiClient((_, _, _) => throw new ApiException(ApiErrorKind.Network, "test")));
        Assert.Contains("Không thể kết nối", (await Assert.ThrowsAsync<WarrantyRequestException>(() => repo.LookupAsync("IMEI", null, null, default))).Message);
    }
}
