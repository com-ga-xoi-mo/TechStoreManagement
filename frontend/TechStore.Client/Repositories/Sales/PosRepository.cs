using System.Net;
using System.Text.Json;
using TechStore.Client.Models.Orders;
using TechStore.Client.Models.Sales;
using TechStore.Client.Repositories.Orders;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using static TechStore.Client.Repositories.Orders.OrderJson;

namespace TechStore.Client.Repositories.Sales;

public sealed class PosRepository(IApiClient apiClient) : IPosRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PosShift?> GetCurrentShiftAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return ParseShift(await apiClient.GetAsync<JsonElement>("api/pos/shifts/current", cancellationToken));
        }
        catch (ApiException exception) when (exception.Kind == ApiErrorKind.Http && exception.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PosShift> OpenShiftAsync(decimal openingBalance, CancellationToken cancellationToken = default)
    {
        var body = new OpenShiftRequest { OpeningBalance = openingBalance };
        return ParseShift(await apiClient.PostAsync<OpenShiftRequest, JsonElement>("api/pos/shifts", body, cancellationToken));
    }

    public async Task<PosShift> CloseShiftAsync(
        Guid shiftId,
        decimal countedCash,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty) throw new ArgumentException("A shift id is required.", nameof(shiftId));

        var body = new CloseShiftRequest { ClosingBalance = countedCash, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() };
        var json = await apiClient.PostAsync<CloseShiftRequest, JsonElement>($"api/pos/shifts/{shiftId:D}/close", body, cancellationToken);
        return ParseShift(json);
    }

    public Task<PosQuoteDto> QuoteAsync(PosCheckoutRequest request, CancellationToken cancellationToken = default) =>
        apiClient.PostAsync<PosCheckoutRequest, PosQuoteDto>("api/pos/quote", request, cancellationToken);

    public async Task<OrderDetail> CheckoutAsync(PosCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<PosCheckoutRequest, JsonElement>("api/pos/checkout", request, cancellationToken);
        return OrderRepository.ParseDetail(json, expectedId: null);
    }

    private static PosShift ParseShift(JsonElement json)
    {
        RequireObject(json, "shift");
        RequireGuid(json, "id");
        RequireGuid(json, "cashierUserId");
        RequireString(json, "status");
        RequireDecimal(json, "openingBalance");
        RequireDecimal(json, "cashSalesTotal");
        RequireDateTime(json, "openedAt");

        PosShiftDto dto;
        try
        {
            dto = json.Deserialize<PosShiftDto>(JsonOptions) ?? throw InvalidResponse("The shift response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("The shift response did not match the API contract.", exception);
        }

        if (dto.OpeningBalance < 0 || dto.CashSalesTotal < 0)
            throw InvalidResponse("The shift contains invalid values.");

        return new PosShift(
            dto.Id,
            dto.CashierUserId,
            dto.CashierName,
            ParseEnum<PosSessionStatus>(dto.Status, "status"),
            dto.OpeningBalance,
            dto.CashSalesTotal,
            dto.ExpectedClosingBalance ?? dto.OpeningBalance + dto.CashSalesTotal,
            dto.ClosingBalance,
            dto.Discrepancy,
            dto.ClosingNote,
            dto.OpenedAt,
            dto.ClosedAt);
    }
}
