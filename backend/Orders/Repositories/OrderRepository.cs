using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Orders.Entities;

namespace TechStore.Api.Orders.Repositories;

public class OrderRepository(AppDbContext dbContext) : IOrderRepository
{
    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> SearchAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Category)
            .AsQueryable();

        if (criteria.Status.HasValue)
        {
            query = query.Where(o => o.Status == criteria.Status.Value);
        }

        if (criteria.CustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == criteria.CustomerId.Value);
        }

        if (criteria.From.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= criteria.From.Value);
        }

        if (criteria.To.HasValue)
        {
            query = query.Where(o => o.CreatedAt < criteria.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var pattern = $"%{EscapeLikePattern(criteria.Keyword)}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.OrderCode, pattern) ||
                (o.Customer != null &&
                    (EF.Functions.ILike(o.Customer.FullName, pattern) ||
                     EF.Functions.ILike(o.Customer.Phone, pattern))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = criteria.SortField switch
        {
            OrderSortField.TotalAmount => criteria.Descending
                ? query.OrderByDescending(o => o.TotalAmount)
                : query.OrderBy(o => o.TotalAmount),
            _ => criteria.Descending
                ? query.OrderByDescending(o => o.CreatedAt)
                : query.OrderBy(o => o.CreatedAt)
        };

        var items = await ordered
            .ThenBy(o => o.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Order?> GetWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.SerialImei)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetForUpdateWithItemsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Lock the order row first, then load its lines; EF fixes up order.Items on the tracked order.
        var order = await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM orders WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            return null;
        }

        await dbContext.OrderItems
            .Where(i => i.OrderId == id)
            .Include(i => i.Product)
            .Include(i => i.Variant)
            .Include(i => i.SerialImei)
            .LoadAsync(cancellationToken);

        return order;
    }

    public async Task<string?> GetLastOrderCodeAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        // Longest code first so a sequence beyond 9999 still sorts after 9999.
        return await dbContext.Orders
            .AsNoTracking()
            .Where(o => o.OrderCode.StartsWith(prefix))
            .OrderByDescending(o => o.OrderCode.Length)
            .ThenByDescending(o => o.OrderCode)
            .Select(o => o.OrderCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(Order order)
    {
        dbContext.Orders.Add(order);
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
    }
}
