using TechStore.Api.Identity.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public interface IReturnService
{
    Task<OrderReturnsDto> OrderAsync(Guid orderId, AuthenticatedUser actor, CancellationToken ct);
    Task<ReturnDto> GetAsync(Guid id, AuthenticatedUser actor, CancellationToken ct);
    Task<ReturnDto> CreateAsync(CreateReturnRequest input, AuthenticatedUser actor, CancellationToken ct);
    Task<ReturnDto> SettleAsync(Guid id, SettleReturnRequest input, AuthenticatedUser actor, CancellationToken ct);
    Task<ReturnDto> ReconcileAsync(Guid id, ReconcileReturnRequest input, AuthenticatedUser actor, CancellationToken ct);
    Task<ReturnDto> ExchangeAsync(Guid id, LinkExchangeRequest input, AuthenticatedUser actor, CancellationToken ct);
    Task<List<ReturnInspectionDto>> InspectionsAsync(CancellationToken ct);
    Task<ReturnInspectionDto> InspectAsync(Guid lineId, InspectReturnRequest input, AuthenticatedUser actor, CancellationToken ct);
}

public sealed class ReturnProblem(int status, string message, string? errorField = null) : Exception(message)
{
    public int Status => status;
    public string? Field => errorField;
    public static ReturnProblem Missing() => new(404, "Không tìm thấy phiếu hoặc đơn hàng.");
    public static ReturnProblem Conflict(string message) => new(409, message);
    public static ReturnProblem Invalid(string field, string message) => new(400, message, field);
}
