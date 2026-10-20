using TechStore.Client.Models.Orders;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Orders;

public interface IOrderRepository
{
    Task<OrderPage> SearchAsync(OrderQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>null</c> when the order does not exist (HTTP 404).</summary>
    Task<OrderDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>POST /api/orders. 400 and 409 reach the caller as <c>ApiException</c>.</summary>
    Task<OrderDetail> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>PATCH /api/orders/{id}/status. 400, 404 and 409 reach the caller as <c>ApiException</c>.</summary>
    Task<OrderDetail> UpdateStatusAsync(Guid id, OrderStatus target, CancellationToken cancellationToken = default);
}

public interface IOrderDeviceRepository
{
    /// <summary>InStock devices of a product (and variant). Only identifiers are returned, never cost.</summary>
    Task<AvailableDevicePage> GetInStockAsync(Guid productId, Guid? variantId, CancellationToken cancellationToken = default);
}
