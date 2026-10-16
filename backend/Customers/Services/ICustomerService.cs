using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers.Services;

public sealed record CustomerOperationResult<T>(
    bool IsSuccess,
    bool Found,
    T? Data,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsConflict)
{
    public static CustomerOperationResult<T> Success(T data) => new(true, true, data, null, false);
    public static CustomerOperationResult<T> NotFound() => new(false, false, default, null, false);
    public static CustomerOperationResult<T> Invalid(Dictionary<string, string[]> errors) => new(false, true, default, errors, false);
    public static CustomerOperationResult<T> Conflict() => new(false, true, default, null, true);
}

public interface ICustomerService
{
    Task<CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<CustomerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>> GetLoyaltyPointsAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<PagedResultDto<OrderSummaryDto>>> GetOrdersAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
    Task<CustomerOperationResult<bool>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);
}
