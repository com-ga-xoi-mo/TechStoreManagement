using TechStore.Client.Models.Orders;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Orders;

public interface IReturnRepository
{
    Task<OrderReturnSummary> OrderAsync(Guid orderId, CancellationToken token);
    Task<ReturnHistory> GetAsync(Guid id, CancellationToken token);
    Task<ReturnHistory> CreateAsync(CreateReturnRequest request, CancellationToken token);
    Task<ReturnHistory> SettleAsync(Guid id, string method, CancellationToken token);
    Task<ReturnHistory> ReconcileAsync(Guid id, ReconcileReturnRequest request, CancellationToken token);
    Task<ReturnHistory> LinkAsync(Guid id, Guid replacementOrderId, CancellationToken token);
    Task<IReadOnlyList<InspectionBatch>> InspectionsAsync(CancellationToken token);
    Task<InspectionBatch> InspectAsync(Guid lineId, InspectReturnRequest request, CancellationToken token);
}

public sealed class ReturnRepository(IApiClient api) : IReturnRepository
{
    public async Task<OrderReturnSummary> OrderAsync(Guid orderId, CancellationToken token)
    {
        var dto = await api.GetAsync<OrderReturnsDto>($"api/returns/order/{orderId:D}", token);
        return new(dto.OrderCode, dto.Balances.Select(line => new ReturnBalance(line.OrderItemId, line.ProductName,
            line.SerialImeiId, line.SerialNumber, line.RemainingQuantity, line.RemainingAmount, line.Sku, line.VariantName)).ToArray(),
            dto.History.Select(Map).ToArray(), dto.LegacyRefunds?.Select(row => new LegacyRefund(row.Id, row.OrderItemId,
                row.Quantity, row.RefundAmount, row.Method, row.RefundedAt, row.Reason, row.IsRestocked)).ToArray(), dto.ReviewReason);
    }

    public async Task<ReturnHistory> GetAsync(Guid id, CancellationToken token) =>
        Map(await api.GetAsync<ReturnDto>($"api/returns/{id:D}", token));

    public async Task<ReturnHistory> CreateAsync(CreateReturnRequest request, CancellationToken token) =>
        Map(await api.PostAsync<CreateReturnRequest, ReturnDto>("api/returns", request, token));

    public async Task<ReturnHistory> SettleAsync(Guid id, string method, CancellationToken token) =>
        Map(await api.PostAsync<SettleReturnRequest, ReturnDto>($"api/returns/{id:D}/settle", new(method), token));

    public async Task<ReturnHistory> ReconcileAsync(Guid id, ReconcileReturnRequest request, CancellationToken token) =>
        Map(await api.PostAsync<ReconcileReturnRequest, ReturnDto>($"api/returns/{id:D}/reconcile", request, token));

    public async Task<ReturnHistory> LinkAsync(Guid id, Guid replacementOrderId, CancellationToken token) =>
        Map(await api.PostAsync<LinkExchangeRequest, ReturnDto>($"api/returns/{id:D}/exchange", new(replacementOrderId), token));

    public async Task<IReadOnlyList<InspectionBatch>> InspectionsAsync(CancellationToken token) =>
        (await api.GetAsync<List<ReturnInspectionDto>>("api/inventory/returns", token)).Select(Map).ToArray();

    public async Task<InspectionBatch> InspectAsync(Guid lineId, InspectReturnRequest request, CancellationToken token) =>
        Map(await api.PostAsync<InspectReturnRequest, ReturnInspectionDto>($"api/inventory/returns/{lineId:D}/inspections", request, token));

    private static ReturnHistory Map(ReturnDto dto) => new(dto.Id, dto.Status, dto.Reason, dto.Method, dto.BankReference,
        dto.ReplacementOrderId, dto.CreatedAt, dto.RefundedAt, dto.Lines.Select(line =>
            new ReturnLine(line.OrderItemId, line.Quantity, line.NetAmount, line.TaxAmount, line.DiscountAmount)).ToArray(), dto.BankApprovedAt);

    private static InspectionBatch Map(ReturnInspectionDto dto) => new(dto.ReturnLineId, dto.RequestId, dto.ProductName,
        dto.SerialNumber ?? dto.SerialImeiId?.ToString("D"), dto.Quantity, dto.Pending, dto.Restocked, dto.Defective, dto.UnderRepair, dto.Sku);
}
