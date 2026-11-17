using FluentAssertions;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.Repositories.Inventory;

public sealed class SerialDeviceRepositoryTests
{
    private sealed class MockApiClient : IApiClient
    {
        public Task<ApiResponse<TResponse>> PostWithStatusAsync<TRequest, TResponse>(
            string relativePath,
            TRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public string? LastPath { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastBody { get; private set; }
        public object? ResponseOrException { get; set; }

        public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default)
        {
            LastPath = relativePath;
            LastMethod = "POST";
            LastBody = request;
            if (ResponseOrException is Exception ex) return Task.FromException<TResponse>(ex);
            return Task.FromResult((TResponse)ResponseOrException!);
        }

        public Task<TResponse> PutAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PostAsync<TRequest>(string relativePath, TRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task PatchAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TResponse> PostFileAsync<TResponse>(string relativePath, ApiFileContent file, IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ReportDefectiveAsync_WhenOk_ReturnsSuccessWithData()
    {
        var api = new MockApiClient();
        var repo = new SerialDeviceRepository(api);
        var deviceId = Guid.NewGuid();
        var returnedDto = new SerialImeiDto
        {
            Id = deviceId,
            Imei = "351234567890123",
            Status = SerialImeiStatus.Defective
        };
        api.ResponseOrException = returnedDto;

        var req = new ReportDefectiveDeviceRequest { Reason = "Hư màn hình", Note = "Rơi vỡ" };
        var result = await repo.ReportDefectiveAsync(deviceId, req);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(SerialImeiStatus.Defective);
        api.LastPath.Should().Be($"api/inventory/serials/{deviceId:D}/defective");
        api.LastBody.Should().Be(req);
    }

    [Fact]
    public async Task ReportDefectiveAsync_WhenHttpError_MapsFailureKinds()
    {
        var api = new MockApiClient();
        var repo = new SerialDeviceRepository(api);
        var deviceId = Guid.NewGuid();
        var req = new ReportDefectiveDeviceRequest { Reason = "Hư hỏng" };

        // 400
        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Validation failed", statusCode: 400, detail: "Lý do không hợp lệ.");
        var r400 = await repo.ReportDefectiveAsync(deviceId, req);
        r400.Succeeded.Should().BeFalse();
        r400.Failure.Should().Be(SerialDeviceFailureKind.BadRequest);
        r400.Message.Should().Be("Lý do không hợp lệ.");

        // 401
        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Unauthorized", statusCode: 401, detail: "Phiên hết hạn.");
        var r401 = await repo.ReportDefectiveAsync(deviceId, req);
        r401.Failure.Should().Be(SerialDeviceFailureKind.Unauthorized);

        // 403
        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Forbidden", statusCode: 403, detail: "Không có quyền.");
        var r403 = await repo.ReportDefectiveAsync(deviceId, req);
        r403.Failure.Should().Be(SerialDeviceFailureKind.Forbidden);

        // 404
        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Not Found", statusCode: 404, detail: "Không tìm thấy thiết bị.");
        var r404 = await repo.ReportDefectiveAsync(deviceId, req);
        r404.Failure.Should().Be(SerialDeviceFailureKind.NotFound);

        // 409
        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Conflict", statusCode: 409, detail: "Thiết bị không ở trạng thái Trong kho.");
        var r409 = await repo.ReportDefectiveAsync(deviceId, req);
        r409.Failure.Should().Be(SerialDeviceFailureKind.Conflict);

        // Network / General
        api.ResponseOrException = new HttpRequestException("Connection refused");
        var rNet = await repo.ReportDefectiveAsync(deviceId, req);
        rNet.Failure.Should().Be(SerialDeviceFailureKind.Network);
    }

    [Fact]
    public async Task RestoreAsync_WhenOk_ReturnsSuccessWithData()
    {
        var api = new MockApiClient();
        var repo = new SerialDeviceRepository(api);
        var deviceId = Guid.NewGuid();
        var returnedDto = new SerialImeiDto
        {
            Id = deviceId,
            Imei = "351234567890123",
            Status = SerialImeiStatus.InStock
        };
        api.ResponseOrException = returnedDto;

        var req = new RestoreDeviceRequest { Note = "Đã thay màn hình" };
        var result = await repo.RestoreAsync(deviceId, req);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(SerialImeiStatus.InStock);
        api.LastPath.Should().Be($"api/inventory/serials/{deviceId:D}/restore");
        api.LastBody.Should().Be(req);
    }

    [Fact]
    public async Task RestoreAsync_WhenConflict_MapsConflictFailure()
    {
        var api = new MockApiClient();
        var repo = new SerialDeviceRepository(api);
        var deviceId = Guid.NewGuid();

        api.ResponseOrException = new ApiException(ApiErrorKind.Http, "Conflict", statusCode: 409, detail: "Thiết bị không ở trạng thái Lỗi.");
        var result = await repo.RestoreAsync(deviceId, new RestoreDeviceRequest());

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().Be(SerialDeviceFailureKind.Conflict);
        result.Message.Should().Be("Thiết bị không ở trạng thái Lỗi.");
    }
}
