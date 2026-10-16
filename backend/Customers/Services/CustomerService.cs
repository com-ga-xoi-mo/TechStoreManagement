using System.Net.Mail;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Orders.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Customers.Services;

public class CustomerService(
    ICustomerRepository customerRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICustomerService
{
    private const string CustomerPhoneConstraint = "idx_customers_phone";

    public async Task<CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
    {
        CustomerTier? tier = null;
        if (!string.IsNullOrWhiteSpace(request.Tier))
        {
            var tierName = request.Tier.Trim();
            if (!Enum.GetNames<CustomerTier>().Any(n => string.Equals(n, tierName, StringComparison.OrdinalIgnoreCase)))
                return CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>.Invalid(new() { ["tier"] = [$"Tier must be one of: {string.Join(", ", Enum.GetNames<CustomerTier>())}."] });
            tier = Enum.Parse<CustomerTier>(tierName, ignoreCase: true);
        }

        var (page, pageSize) = NormalizePaging(request.Page, request.PageSize);
        var criteria = new CustomerSearchCriteria(string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), tier, request.IsActive, page, pageSize);
        var (items, count) = await customerRepository.SearchAsync(criteria, cancellationToken);
        var data = new PagedResultDto<CustomerSummaryDto>(items.Select(MapSummary).ToList(), page, pageSize, count, TotalPages(count, pageSize));
        return CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>.Success(data);
    }

    public async Task<CustomerOperationResult<CustomerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetByIdAsync(id, cancellationToken);
        return customer == null ? CustomerOperationResult<CustomerDto>.NotFound() : CustomerOperationResult<CustomerDto>.Success(Map(customer));
    }

    public async Task<CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>> GetLoyaltyPointsAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        if (await customerRepository.GetByIdAsync(id, cancellationToken) == null)
            return CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>.NotFound();
        var (normalizedPage, normalizedSize) = NormalizePaging(page, pageSize);
        var (items, count) = await customerRepository.GetLoyaltyPointsAsync(new(id, normalizedPage, normalizedSize), cancellationToken);
        var data = new PagedResultDto<CustomerLoyaltyPointDto>(items.Select(p => new CustomerLoyaltyPointDto
        {
            Id = p.Id, PointsChange = p.PointsChange, BalanceAfter = p.BalanceAfter, Reason = p.Reason, OrderId = p.OrderId, CreatedAt = p.CreatedAt
        }).ToList(), normalizedPage, normalizedSize, count, TotalPages(count, normalizedSize));
        return CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>.Success(data);
    }

    public async Task<CustomerOperationResult<PagedResultDto<OrderSummaryDto>>> GetOrdersAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        if (await customerRepository.GetByIdAsync(id, cancellationToken) == null)
            return CustomerOperationResult<PagedResultDto<OrderSummaryDto>>.NotFound();
        var (normalizedPage, normalizedSize) = NormalizePaging(page, pageSize);
        var criteria = new OrderSearchCriteria(null, id, null, null, null, OrderSortField.CreatedAt, true, normalizedPage, normalizedSize);
        var (items, count) = await orderRepository.SearchAsync(criteria, cancellationToken);
        var summaries = items.Select(o => new OrderSummaryDto
        {
            Id = o.Id, OrderCode = o.OrderCode, Status = o.Status.ToString(), PaymentMethod = o.PaymentMethod.ToString(),
            CustomerId = o.CustomerId, CustomerName = o.Customer?.FullName, TotalAmount = o.TotalAmount,
            ItemCount = o.Items.Count, CreatedAt = o.CreatedAt, PaidAt = o.PaidAt, CompletedAt = o.CompletedAt
        }).ToList();
        return CustomerOperationResult<PagedResultDto<OrderSummaryDto>>.Success(new(summaries, normalizedPage, normalizedSize, count, TotalPages(count, normalizedSize)));
    }

    public async Task<CustomerOperationResult<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(request, timeProvider.GetUtcNow().UtcDateTime);
        if (normalized.Errors.Count > 0) return CustomerOperationResult<CustomerDto>.Invalid(normalized.Errors);
        if (await customerRepository.PhoneExistsAsync(normalized.Phone!, cancellationToken: cancellationToken)) return CustomerOperationResult<CustomerDto>.Conflict();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var customer = new Customer
        {
            FullName = normalized.FullName!, Phone = normalized.Phone!, Email = normalized.Email,
            DateOfBirth = request.DateOfBirth, Address = normalized.Address, LoyaltyPoints = 0,
            Tier = CustomerTier.Standard, IsActive = true, CreatedAt = now
        };
        customerRepository.Add(customer);
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == CustomerPhoneConstraint) { return CustomerOperationResult<CustomerDto>.Conflict(); }
        return CustomerOperationResult<CustomerDto>.Success(Map(customer));
    }

    public async Task<CustomerOperationResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(request, timeProvider.GetUtcNow().UtcDateTime);
        if (normalized.Errors.Count > 0) return CustomerOperationResult<CustomerDto>.Invalid(normalized.Errors);
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (customer == null) return CustomerOperationResult<CustomerDto>.NotFound();
        if (await customerRepository.PhoneExistsAsync(normalized.Phone!, id, cancellationToken)) return CustomerOperationResult<CustomerDto>.Conflict();
        customer.FullName = normalized.FullName!;
        customer.Phone = normalized.Phone!;
        customer.Email = normalized.Email;
        customer.DateOfBirth = request.DateOfBirth;
        customer.Address = normalized.Address;
        customer.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        try { await unitOfWork.SaveChangesAsync(cancellationToken); }
        catch (DuplicateKeyException ex) when (ex.ConstraintName == CustomerPhoneConstraint) { return CustomerOperationResult<CustomerDto>.Conflict(); }
        return CustomerOperationResult<CustomerDto>.Success(Map(customer));
    }

    public async Task<CustomerOperationResult<bool>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await customerRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (customer == null) return CustomerOperationResult<bool>.NotFound();
        if (customer.IsActive)
        {
            customer.IsActive = false;
            customer.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        return CustomerOperationResult<bool>.Success(true);
    }

    private static (string? FullName, string? Phone, string? Email, string? Address, Dictionary<string, string[]> Errors) NormalizeAndValidate(CreateCustomerRequest request, DateTime now)
    {
        var errors = new Dictionary<string, List<string>>();
        void Error(string field, string message) { if (!errors.TryGetValue(field, out var list)) errors[field] = list = []; list.Add(message); }
        var name = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (string.IsNullOrWhiteSpace(name)) Error("fullName", "Full name is required.");
        if (string.IsNullOrWhiteSpace(phone)) Error("phone", "Phone is required.");
        if (email != null && (!MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))) Error("email", "Email must be a valid email address.");
        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) > now.Date) Error("dateOfBirth", "Date of birth cannot be in the future.");
        return (name, phone, email, address, errors.ToDictionary(k => k.Key, v => v.Value.ToArray()));
    }

    private static CustomerSummaryDto MapSummary(Customer c) => new()
    {
        Id = c.Id, FullName = c.FullName, Phone = c.Phone, LoyaltyPoints = c.LoyaltyPoints,
        Tier = c.Tier.ToString(), IsActive = c.IsActive, CreatedAt = c.CreatedAt
    };

    private static CustomerDto Map(Customer c) => new()
    {
        Id = c.Id, FullName = c.FullName, Phone = c.Phone, Email = c.Email, DateOfBirth = c.DateOfBirth,
        Address = c.Address, LoyaltyPoints = c.LoyaltyPoints, Tier = c.Tier.ToString(), IsActive = c.IsActive,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };

    private static (int Page, int PageSize) NormalizePaging(int? page, int? pageSize) =>
        (page is > 0 ? page.Value : 1, pageSize is null ? 20 : Math.Clamp(pageSize.Value, 1, 100));

    private static int TotalPages(int count, int size) => count == 0 ? 0 : (int)Math.Ceiling((double)count / size);
}
