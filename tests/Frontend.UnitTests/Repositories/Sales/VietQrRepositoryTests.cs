using TechStore.Client.Repositories.Sales;
using TechStore.Client.Services.Api;
using TechStore.Client.UnitTests.Repositories.Orders;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.UnitTests.Repositories.Sales;

public sealed class VietQrRepositoryTests
{
    private const string Id = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string EventId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    private static string Payment(string status = "Pending", string id = Id, string image = "\"iVBORw0KGgo=\"", string modules = "\"1010\\n0101\"") => $$"""
        {"id":"{{id}}","orderId":"cccccccc-cccc-cccc-cccc-cccccccccccc","orderCode":"ORD-202611-0042","status":"{{status}}","orderStatus":"Pending",
         "amount":11000000,"transactionCode":"TSABCDEFGH23","bankBin":"970422","bankAccountNumber":"0123456789","bankAccountName":"TECHSTORE",
         "qrContent":"000201","qrImagePng":{{image}},"qrModules":{{modules}},"createdAt":"2026-11-25T08:00:00Z","expiresAt":"2026-11-25T08:15:00Z","secondsLeft":900,
         "posSessionId":"dddddddd-dddd-dddd-dddd-dddddddddddd","cashierUserId":null,"cashierName":"Thu ngan","confirmedAt":null}
        """;

    private static string EventJson(string status = "NeedsReconciliation", string reason = "LateAfterExpiry", string id = EventId) => $$"""
        {"id":"{{id}}","provider":"sandbox","providerTransactionId":"FT1","amount":11000000,"transferContent":"CK TSABCDEFGH23",
         "accountNumber":"0123456789","transferredAt":"2026-11-25T08:20:00Z","receivedAt":"2026-11-25T08:20:01Z","vietQrTransactionId":null,
         "orderCode":"ORD-202611-0042","status":"{{status}}","reason":"{{reason}}","resolvedByUserId":null,"resolvedAt":null,"resolutionNote":null}
        """;

    private static string Page(string item) => $$"""{"items":[{{item}}],"page":1,"pageSize":50,"totalCount":1,"totalPages":1}""";

    [Fact]
    public async Task CreateAsync_PostsTheRequestAndMapsTheCodeWithItsImage()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Payment());
        var request = new CreateVietQrRequest { CheckoutKey = Guid.NewGuid(), ExpectedTotal = 11_000_000m };

        // Act
        var payment = await new VietQrRepository(api).CreateAsync(request);

        // Assert
        Assert.Equal(("POST", "api/pos/vietqr"), (api.Single.Method, api.Single.Path));
        Assert.Same(request, api.Single.Body);
        Assert.Equal(VietQrStatus.Pending, payment.Status);
        Assert.Equal(11_000_000m, payment.Amount);
        Assert.Equal("iVBORw0KGgo=", payment.QrImagePng);
        Assert.Equal("1010\n0101", payment.QrModules);
        Assert.True(payment.IsPending);
    }

    [Fact]
    public async Task CreateAsync_WithoutTheQrModules_IsAnInvalidResponse()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Payment(modules: "null"));

        // Act
        var error = await Assert.ThrowsAsync<ApiException>(() => new VietQrRepository(api).CreateAsync(new CreateVietQrRequest()));

        // Assert
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetAsync_ReadsTheRequestById()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Payment("Confirmed"));

        // Act
        var payment = await new VietQrRepository(api).GetAsync(Guid.Parse(Id));

        // Assert
        Assert.Equal($"api/pos/vietqr/{Id}", api.Single.Path);
        Assert.Equal(VietQrStatus.Confirmed, payment.Status);
    }

    [Fact]
    public async Task GetAsync_AnswerForAnotherId_IsAnInvalidResponse()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Payment(id: "99999999-9999-9999-9999-999999999999"));

        // Act and Assert
        var error = await Assert.ThrowsAsync<ApiException>(() => new VietQrRepository(api).GetAsync(Guid.Parse(Id)));
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task GetAsync_UnknownStatusOrBrokenJson_IsAnInvalidResponse()
    {
        // Act and Assert
        var unknown = await Assert.ThrowsAsync<ApiException>(() =>
            new VietQrRepository(new RecordingApiClient((_, _, _) => Payment("7"))).GetAsync(Guid.Parse(Id)));
        var broken = await Assert.ThrowsAsync<ApiException>(() =>
            new VietQrRepository(new RecordingApiClient((_, _, _) => """{"id":"not a guid"}""")).GetAsync(Guid.Parse(Id)));
        Assert.Equal(ApiErrorKind.InvalidResponse, unknown.Kind);
        Assert.Equal(ApiErrorKind.InvalidResponse, broken.Kind);
    }

    [Fact]
    public async Task ListAsync_SendsTheFiltersAsQueryParameters()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(Payment(image: "null")));
        var shift = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        // Act
        var list = await new VietQrRepository(api).ListAsync(VietQrStatus.Pending, shift);

        // Assert
        Assert.Equal($"api/pos/vietqr?status=Pending&posSessionId={shift}&page=1&pageSize=50", api.Single.Path);
        Assert.Single(list);
        Assert.Null(list[0].QrImagePng);
    }

    [Fact]
    public async Task CancelAsync_PostsToTheCancelRoute()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => "{}");

        // Act
        await new VietQrRepository(api).CancelAsync(Guid.Parse(Id));

        // Assert
        Assert.Equal(("POST", $"api/pos/vietqr/{Id}/cancel"), (api.Single.Method, api.Single.Path));
    }

    [Fact]
    public async Task CancelAsync_Conflict_ReachesTheCaller()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => throw RecordingApiClient.Http(409));

        // Act and Assert
        var error = await Assert.ThrowsAsync<ApiException>(() => new VietQrRepository(api).CancelAsync(Guid.Parse(Id)));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public async Task ConfirmAsync_PostsTheTrimmedReferenceAndAmount()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Payment("Confirmed"));

        // Act
        var payment = await new VietQrRepository(api).ConfirmAsync(Guid.Parse(Id), "  FT26112500123 ", 11_000_000m);

        // Assert
        Assert.Equal($"api/pos/vietqr/{Id}/confirm", api.Single.Path);
        var body = Assert.IsType<ConfirmVietQrRequest>(api.Single.Body);
        Assert.Equal(("FT26112500123", 11_000_000m), (body.ReferenceCode, body.ReceivedAmount));
        Assert.Equal(VietQrStatus.Confirmed, payment.Status);
    }

    [Fact]
    public async Task ListEventsAsync_FiltersByStatusAndMapsTheReason()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(EventJson()));

        // Act
        var events = await new VietQrRepository(api).ListEventsAsync(VietQrEventStatus.NeedsReconciliation);

        // Assert
        Assert.Equal("api/payments/vietqr/events?status=NeedsReconciliation&page=1&pageSize=50", api.Single.Path);
        var item = Assert.Single(events);
        Assert.Equal(VietQrEventReason.LateAfterExpiry, item.Reason);
        Assert.True(item.NeedsReconciliation);
    }

    [Fact]
    public async Task ListEventsAsync_UnknownReason_IsAnInvalidResponse()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => Page(EventJson(reason: "Whatever")));

        // Act and Assert
        var error = await Assert.ThrowsAsync<ApiException>(() => new VietQrRepository(api).ListEventsAsync(null));
        Assert.Equal(ApiErrorKind.InvalidResponse, error.Kind);
    }

    [Fact]
    public async Task ResolveEventAsync_PostsTheTrimmedNote()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => EventJson("Resolved"));

        // Act
        var resolved = await new VietQrRepository(api).ResolveEventAsync(Guid.Parse(EventId), " Hoàn tiền cho khách ");

        // Assert
        Assert.Equal($"api/payments/vietqr/events/{EventId}/resolve", api.Single.Path);
        Assert.Equal("Hoàn tiền cho khách", Assert.IsType<ResolveVietQrEventRequest>(api.Single.Body).Note);
        Assert.Equal(VietQrEventStatus.Resolved, resolved.Status);
    }

    [Fact]
    public async Task EmptyIds_AreRejectedBeforeAnyCall()
    {
        // Arrange
        var api = new RecordingApiClient((_, _, _) => "{}");
        var repository = new VietQrRepository(api);

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CancelAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.ConfirmAsync(Guid.Empty, "FT1", 1m));
        Assert.Empty(api.Calls);
    }
}
