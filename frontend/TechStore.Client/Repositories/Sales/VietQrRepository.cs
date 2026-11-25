using System.Text.Json;
using TechStore.Client.Models.Sales;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using static TechStore.Client.Repositories.Orders.OrderJson;

namespace TechStore.Client.Repositories.Sales;

public sealed class VietQrRepository(IApiClient apiClient) : IVietQrRepository
{
    private const int ListSize = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The cancel endpoint takes no body; the client still needs a JSON value to send.</summary>
    private sealed record NoBody;

    public async Task<VietQrPayment> CreateAsync(CreateVietQrRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var json = await apiClient.PostAsync<CreateVietQrRequest, JsonElement>("api/pos/vietqr", request, cancellationToken);
        var payment = MapPayment(Deserialize<VietQrPaymentDto>(json, "VietQR request"));
        if (string.IsNullOrWhiteSpace(payment.QrImagePng)) throw InvalidResponse("The new VietQR request has no QR image.");
        return payment;
    }

    public async Task<VietQrPayment> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureId(id);
        var json = await apiClient.GetAsync<JsonElement>($"api/pos/vietqr/{id:D}", cancellationToken);
        return SameId(MapPayment(Deserialize<VietQrPaymentDto>(json, "VietQR request")), id);
    }

    public async Task<IReadOnlyList<VietQrPayment>> ListAsync(VietQrStatus? status, Guid? posSessionId, CancellationToken cancellationToken = default)
    {
        var values = new List<string>();
        if (status.HasValue) values.Add($"status={status.Value}");
        if (posSessionId.HasValue) values.Add($"posSessionId={posSessionId.Value:D}");
        values.Add("page=1");
        values.Add($"pageSize={ListSize}");
        var json = await apiClient.GetAsync<JsonElement>($"api/pos/vietqr?{string.Join('&', values)}", cancellationToken);
        var page = Deserialize<PagedResultDto<VietQrPaymentDto>>(json, "VietQR list");
        return page.Items?.Select(MapPayment).ToArray() ?? throw InvalidResponse("The VietQR list has no items.");
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureId(id);
        await apiClient.PostAsync<NoBody, JsonElement>($"api/pos/vietqr/{id:D}/cancel", new NoBody(), cancellationToken);
    }

    public async Task<VietQrPayment> ConfirmAsync(Guid id, string referenceCode, decimal receivedAmount, CancellationToken cancellationToken = default)
    {
        EnsureId(id);
        var body = new ConfirmVietQrRequest { ReferenceCode = referenceCode?.Trim() ?? string.Empty, ReceivedAmount = receivedAmount };
        var json = await apiClient.PostAsync<ConfirmVietQrRequest, JsonElement>($"api/pos/vietqr/{id:D}/confirm", body, cancellationToken);
        return SameId(MapPayment(Deserialize<VietQrPaymentDto>(json, "confirmed VietQR request")), id);
    }

    public async Task<IReadOnlyList<VietQrBankEvent>> ListEventsAsync(VietQrEventStatus? status, CancellationToken cancellationToken = default)
    {
        var path = $"api/payments/vietqr/events?{(status.HasValue ? $"status={status.Value}&" : string.Empty)}page=1&pageSize={ListSize}";
        var json = await apiClient.GetAsync<JsonElement>(path, cancellationToken);
        var page = Deserialize<PagedResultDto<VietQrBankEventDto>>(json, "VietQR event list");
        return page.Items?.Select(MapEvent).ToArray() ?? throw InvalidResponse("The VietQR event list has no items.");
    }

    public async Task<VietQrBankEvent> ResolveEventAsync(Guid id, string note, CancellationToken cancellationToken = default)
    {
        EnsureId(id);
        var body = new ResolveVietQrEventRequest { Note = note?.Trim() ?? string.Empty };
        var json = await apiClient.PostAsync<ResolveVietQrEventRequest, JsonElement>($"api/payments/vietqr/events/{id:D}/resolve", body, cancellationToken);
        var resolved = MapEvent(Deserialize<VietQrBankEventDto>(json, "resolved VietQR event"));
        return resolved.Id == id ? resolved : throw InvalidResponse("The event id did not match the requested id.");
    }

    private static VietQrPayment SameId(VietQrPayment payment, Guid expectedId) =>
        payment.Id == expectedId ? payment : throw InvalidResponse("The VietQR request id did not match the requested id.");

    private static VietQrPayment MapPayment(VietQrPaymentDto dto)
    {
        if (dto.Id == Guid.Empty || dto.OrderId == Guid.Empty
            || string.IsNullOrWhiteSpace(dto.OrderCode) || string.IsNullOrWhiteSpace(dto.TransactionCode)
            || string.IsNullOrWhiteSpace(dto.QrContent) || dto.Amount <= 0 || dto.ExpiresAt <= dto.CreatedAt)
            throw InvalidResponse("A VietQR request contains invalid values.");
        return new(dto.Id, dto.OrderId, dto.OrderCode, ParseEnum<VietQrStatus>(dto.Status, "status"), dto.Amount,
            dto.TransactionCode, dto.BankBin, dto.BankAccountNumber, dto.BankAccountName, dto.QrContent, dto.QrImagePng,
            dto.CreatedAt, dto.ExpiresAt, dto.PosSessionId, dto.CashierName, dto.ConfirmedAt);
    }

    private static VietQrBankEvent MapEvent(VietQrBankEventDto dto)
    {
        if (dto.Id == Guid.Empty || string.IsNullOrWhiteSpace(dto.ProviderTransactionId) || dto.Amount < 0)
            throw InvalidResponse("A VietQR event contains invalid values.");
        return new(dto.Id, dto.Provider, dto.ProviderTransactionId, dto.Amount, dto.TransferContent, dto.AccountNumber,
            dto.TransferredAt, dto.OrderCode, ParseEnum<VietQrEventStatus>(dto.Status, "status"),
            ParseEnum<VietQrEventReason>(dto.Reason, "reason"), dto.ResolvedAt, dto.ResolutionNote);
    }

    private static T Deserialize<T>(JsonElement json, string description) where T : class
    {
        try
        {
            return json.Deserialize<T>(JsonOptions) ?? throw InvalidResponse($"The {description} response was null.");
        }
        catch (JsonException exception)
        {
            throw InvalidResponse($"The {description} response did not match the API contract.", exception);
        }
    }

    private static void EnsureId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("An id is required.", nameof(id));
    }
}
