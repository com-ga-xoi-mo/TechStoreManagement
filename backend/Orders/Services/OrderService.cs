using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Api.Customers.Entities;
using TechStore.Api.Customers.Repositories;
using TechStore.Api.Inventory.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Api.Orders.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    ICustomerRepository customerRepository,
    IInventoryFulfillmentService fulfillmentService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IOrderService
{
    private const int MaxCodeAttempts = 5;
    private const string OrderCodeConstraint = "idx_orders_code";

    // A request line that passed every check and is ready to be priced and stored.
    private sealed record BuiltLine(
        int Index,
        Product Product,
        ProductVariant? Variant,
        Guid? SerialImeiId,
        int Quantity,
        decimal UnitPrice,
        decimal Discount);

    // ------------------------------------------------------------------
    // UC-ORD-01: create a Pending order
    // ------------------------------------------------------------------

    public async Task<CreateOrderResult> CreateAsync(
        CreateOrderRequest request,
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var (field, messages) in CreateOrderValidator.Validate(request))
        {
            errors[field] = messages.ToList();
        }

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = [];
                errors[field] = list;
            }
            list.Add(message);
        }

        Dictionary<string, string[]> Snapshot() => errors.ToDictionary(k => k.Key, v => v.Value.ToArray());

        // Without usable lines nothing else can be checked.
        if (request?.Items == null || request.Items.Count == 0 || request.Items.Count > CreateOrderValidator.MaxLines)
        {
            return CreateOrderResult.ValidationFailed(Snapshot());
        }

        // Customer (optional)
        Customer? customer = null;
        if (request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            customer = await customerRepository.GetByIdAsync(request.CustomerId.Value, cancellationToken);
            if (customer == null || !customer.IsActive)
            {
                AddError("customerId", "Customer was not found or is not active.");
            }
        }

        // Lines
        var productCache = new Dictionary<Guid, Product?>();
        var built = new List<BuiltLine>();
        var nonTrackedKeys = new HashSet<(Guid ProductId, Guid? VariantId)>();

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var prefix = $"items[{i}]";

            if (item == null || !item.ProductId.HasValue || item.ProductId.Value == Guid.Empty)
            {
                continue; // already reported by the validator
            }

            if (!productCache.TryGetValue(item.ProductId.Value, out var product))
            {
                product = await productRepository.GetWithVariantsAsync(item.ProductId.Value, cancellationToken);
                productCache[item.ProductId.Value] = product;
            }

            if (product == null)
            {
                AddError($"{prefix}.productId", "Product was not found.");
                continue;
            }

            if (!product.IsActive)
            {
                AddError($"{prefix}.productId", "Product is not active.");
                continue;
            }

            // Variant rules
            ProductVariant? variant = null;
            var variantProblem = false;
            if (product.Variants.Count > 0)
            {
                if (!item.VariantId.HasValue)
                {
                    AddError($"{prefix}.variantId", "A variant is required for this product.");
                    variantProblem = true;
                }
                else if (item.VariantId.Value != Guid.Empty)
                {
                    variant = product.Variants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    if (variant == null)
                    {
                        AddError($"{prefix}.variantId", "Variant does not belong to the selected product.");
                        variantProblem = true;
                    }
                    else if (!variant.IsActive)
                    {
                        AddError($"{prefix}.variantId", "Variant is not active.");
                        variantProblem = true;
                    }
                }
                else
                {
                    variantProblem = true; // empty guid reported by the validator
                }
            }
            else if (item.VariantId.HasValue && item.VariantId.Value != Guid.Empty)
            {
                AddError($"{prefix}.variantId", "This product has no variants.");
                variantProblem = true;
            }

            if (variantProblem)
            {
                continue;
            }

            // Serial-tracked versus quantity-based lines
            int quantity;
            if (product.IsSerialTracked)
            {
                if (!item.SerialImeiId.HasValue)
                {
                    AddError($"{prefix}.serialImeiId", "A device (serial/IMEI) is required for this product.");
                }

                if (item.Quantity.HasValue && item.Quantity.Value > 1)
                {
                    AddError($"{prefix}.quantity", "Quantity must be 1 for a serial-tracked product.");
                }

                quantity = 1;
            }
            else
            {
                if (item.SerialImeiId.HasValue)
                {
                    AddError($"{prefix}.serialImeiId", "This product is not serial-tracked; do not supply a device.");
                }

                quantity = Math.Max(1, item.Quantity ?? 1);

                if (!nonTrackedKeys.Add((product.Id, variant?.Id)))
                {
                    AddError($"{prefix}.productId", "The same product and variant appear on more than one line; combine the quantities.");
                }
            }

            var unitPrice = OrderPricingHelper.GetUnitPrice(product, variant);
            var discount = item.DiscountAmount ?? 0m;
            if (discount > unitPrice * quantity)
            {
                AddError($"{prefix}.discountAmount", "Discount cannot exceed the line amount.");
            }

            built.Add(new BuiltLine(i, product, variant, item.SerialImeiId, quantity, unitPrice, discount));
        }

        if (errors.Count > 0)
        {
            return CreateOrderResult.ValidationFailed(Snapshot());
        }

        // Availability (unlocked, advisory: transitions re-check under locks)
        var fulfillmentLines = built
            .Select(b => new FulfillmentLine(null, b.Product.Id, b.Variant?.Id, b.SerialImeiId, b.Quantity))
            .ToList();
        var check = await fulfillmentService.CheckAvailabilityAsync(fulfillmentLines, cancellationToken);
        if (!check.IsAvailable)
        {
            foreach (var problem in check.Problems)
            {
                var line = built[problem.LineIndex];
                var field = problem.Kind switch
                {
                    FulfillmentProblemKind.StockNotInitialized => "productId",
                    FulfillmentProblemKind.InsufficientStock => line.SerialImeiId.HasValue ? "serialImeiId" : "quantity",
                    _ => "serialImeiId"
                };
                AddError($"items[{line.Index}].{field}", problem.Message);
            }

            return CreateOrderResult.ValidationFailed(Snapshot());
        }

        // Price and persist
        OrderEnumParser.TryParse<PaymentMethod>(request.PaymentMethod, out var parsedMethod);
        var paymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? PaymentMethod.Cash : parsedMethod;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        var pricedLines = built
            .Select(b => OrderPricingHelper.PriceLine(b.UnitPrice, b.Quantity, b.Discount))
            .ToList();
        var totals = OrderPricingHelper.Calculate(pricedLines);

        Order BuildOrder(DateTime now)
        {
            var order = new Order
            {
                CustomerId = customer?.Id,
                CashierUserId = cashierUserId,
                Status = OrderStatus.Pending,
                PaymentMethod = paymentMethod,
                Subtotal = totals.Subtotal,
                DiscountAmount = totals.DiscountAmount,
                TaxAmount = totals.TaxAmount,
                TotalAmount = totals.TotalAmount,
                Notes = notes,
                CreatedAt = now
            };

            for (var i = 0; i < built.Count; i++)
            {
                var b = built[i];
                order.Items.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = b.Product.Id,
                    VariantId = b.Variant?.Id,
                    SerialImeiId = b.SerialImeiId,
                    ProductName = b.Product.Name,
                    VariantName = b.Variant?.VariantName,
                    Sku = b.Variant?.Sku ?? b.Product.Sku,
                    UnitPrice = pricedLines[i].UnitPrice,
                    UnitCost = null,
                    Quantity = pricedLines[i].Quantity,
                    DiscountAmount = pricedLines[i].DiscountAmount,
                    TotalPrice = pricedLines[i].TotalPrice,
                    CreatedAt = now
                });
            }

            return order;
        }

        for (var attempt = 1; attempt <= MaxCodeAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var order = BuildOrder(now);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var lastCode = await orderRepository.GetLastOrderCodeAsync(OrderCodeGenerator.GetPrefix(now), cancellationToken);
                order.OrderCode = OrderCodeGenerator.Next(now, lastCode);
                orderRepository.Add(order);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await unitOfWork.CommitAsync(cancellationToken);
            }
            catch (DuplicateKeyException ex) when (ex.ConstraintName == OrderCodeConstraint)
            {
                // Lost the race for this code: roll back (clears the tracker) and try again with a fresh code.
                await unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch (DuplicateKeyException)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return CreateOrderResult.Conflict("The order conflicts with existing data (for example a device already used by this order).");
            }
            catch
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }

            var saved = await orderRepository.GetWithItemsAsync(order.Id, cancellationToken) ?? order;
            return CreateOrderResult.Success(MapToDto(saved));
        }

        return CreateOrderResult.Conflict("Could not allocate a unique order code. Please try again.");
    }

    // ------------------------------------------------------------------
    // UC-ORD-03: order detail
    // ------------------------------------------------------------------

    public async Task<OrderDetailResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetWithItemsAsync(id, cancellationToken);
        return order == null
            ? OrderDetailResult.NotFound()
            : OrderDetailResult.Success(MapToDto(order));
    }

    // ------------------------------------------------------------------
    // UC-ORD-02: list orders
    // ------------------------------------------------------------------

    public async Task<OrderSearchResult> SearchAsync(
        OrderSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (OrderEnumParser.TryParse<OrderStatus>(request.Status, out var parsedStatus))
            {
                status = parsedStatus;
            }
            else
            {
                errors["status"] = [$"Status must be one of: {string.Join(", ", Enum.GetNames<OrderStatus>())}."];
            }
        }

        if (request.CustomerId.HasValue && request.CustomerId.Value == Guid.Empty)
        {
            errors["customerId"] = ["Customer ID is invalid."];
        }

        var from = request.From.HasValue ? NormalizeUtc(request.From.Value) : (DateTime?)null;
        var to = request.To.HasValue ? NormalizeUtc(request.To.Value) : (DateTime?)null;
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            errors["from"] = ["'from' must not be later than 'to'."];
        }

        if (!TryParseSort(request.Sort, out var sortField, out var descending))
        {
            errors["sort"] = ["Sort must be one of: createdAt:asc, createdAt:desc, totalAmount:asc, totalAmount:desc."];
        }

        if (errors.Count > 0)
        {
            return OrderSearchResult.ValidationFailed(errors);
        }

        var (page, pageSize) = ProductPagingHelper.Normalize(request.Page, request.PageSize);
        var keyword = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();

        var criteria = new OrderSearchCriteria(
            status,
            request.CustomerId,
            from,
            to,
            keyword,
            sortField,
            descending,
            page,
            pageSize);

        var (items, totalCount) = await orderRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = ProductPagingHelper.CalculateTotalPages(totalCount, pageSize);

        var summaries = items.Select(MapToSummary).ToList();
        return OrderSearchResult.Success(new PagedResultDto<OrderSummaryDto>(summaries, page, pageSize, totalCount, totalPages));
    }

    // ------------------------------------------------------------------
    // UC-ORD-04: status transitions
    // ------------------------------------------------------------------

    public async Task<OrderTransitionResult> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!OrderStatusTransitions.TryParseTarget(request?.TargetStatus, out var target))
        {
            return OrderTransitionResult.ValidationFailed(new Dictionary<string, string[]>
            {
                ["targetStatus"] = ["Target status must be one of: Processing, Completed, Cancelled."]
            });
        }

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetForUpdateWithItemsAsync(id, cancellationToken);
            if (order == null)
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return OrderTransitionResult.NotFound();
            }

            if (!OrderStatusTransitions.IsAllowed(order.Status, target))
            {
                await unitOfWork.RollbackAsync(cancellationToken);
                return OrderTransitionResult.Conflict($"An order that is {order.Status} cannot be moved to {target}.");
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;

            if (target == OrderStatus.Cancelled)
            {
                // Only Pending orders reach this branch; they hold no reservation.
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = now;
            }
            else
            {
                var items = order.Items
                    .OrderBy(i => i.ProductName, StringComparer.Ordinal)
                    .ThenBy(i => i.Sku, StringComparer.Ordinal)
                    .ThenBy(i => i.Id)
                    .ToList();
                var lines = items
                    .Select(i => new FulfillmentLine(i.Id, i.ProductId, i.VariantId, i.SerialImeiId, i.Quantity))
                    .ToList();

                var firstPayment = order.Status == OrderStatus.Pending;

                if (firstPayment)
                {
                    // Payment step: the product must still be on sale, then devices and stock are reserved.
                    var inactive = items.FirstOrDefault(i => !i.Product.IsActive || (i.Variant != null && !i.Variant.IsActive));
                    if (inactive != null)
                    {
                        await unitOfWork.RollbackAsync(cancellationToken);
                        return OrderTransitionResult.Conflict(
                            $"{inactive.ProductName} ({inactive.Sku}) is no longer on sale.");
                    }

                    var reserve = await fulfillmentService.ReserveAsync(lines, cancellationToken);
                    if (!reserve.IsSuccess)
                    {
                        await unitOfWork.RollbackAsync(cancellationToken);
                        return OrderTransitionResult.Conflict(Describe(items, reserve.Conflict!));
                    }

                }

                if (target == OrderStatus.Completed)
                {
                    var sale = await fulfillmentService.ConfirmSaleAsync(lines, now, performedByUserId, cancellationToken);
                    if (!sale.IsSuccess)
                    {
                        await unitOfWork.RollbackAsync(cancellationToken);
                        return OrderTransitionResult.Conflict(Describe(items, sale.Conflict!));
                    }

                    foreach (var item in items)
                    {
                        item.UnitCost = sale.DeviceUnitCosts.GetValueOrDefault(item.Id) ?? item.Product.CostPrice;
                        item.UpdatedAt = now;
                    }

                }

                // Every Inventory step succeeded: only now change the order itself.
                if (firstPayment)
                {
                    order.PaidAt = now;
                }

                if (target == OrderStatus.Completed)
                {
                    order.CompletedAt = now;
                }

                order.Status = target;
                order.UpdatedAt = now;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var updated = await orderRepository.GetWithItemsAsync(id, cancellationToken);
        return updated == null
            ? OrderTransitionResult.NotFound()
            : OrderTransitionResult.Success(MapToDto(updated));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string Describe(IReadOnlyList<OrderItem> items, FulfillmentProblem problem)
    {
        var item = items[problem.LineIndex];
        return $"Line {problem.LineIndex + 1} ({item.Sku}): {problem.Message}";
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static bool TryParseSort(string? sort, out OrderSortField field, out bool descending)
    {
        field = OrderSortField.CreatedAt;
        descending = true;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var parts = sort.Trim().Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Equals("createdAt", StringComparison.OrdinalIgnoreCase))
        {
            field = OrderSortField.CreatedAt;
        }
        else if (parts[0].Equals("totalAmount", StringComparison.OrdinalIgnoreCase))
        {
            field = OrderSortField.TotalAmount;
        }
        else
        {
            return false;
        }

        if (parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            descending = false;
        }
        else if (parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            descending = true;
        }
        else
        {
            return false;
        }

        return true;
    }

    private static OrderSummaryDto MapToSummary(Order order)
    {
        var primaryItem = order.Items.OrderBy(item => item.Id).FirstOrDefault();
        return new OrderSummaryDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            Status = order.Status.ToString(),
            PaymentMethod = order.PaymentMethod.ToString(),
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.FullName,
            TotalAmount = order.TotalAmount,
            ItemCount = order.Items.Count,
            PrimaryProductName = primaryItem?.ProductName,
            PrimaryVariantName = primaryItem?.VariantName,
            PrimaryCategoryName = primaryItem?.Product?.Category?.Name,
            CreatedAt = order.CreatedAt,
            PaidAt = order.PaidAt,
            CompletedAt = order.CompletedAt
        };
    }

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        OrderCode = order.OrderCode,
        Status = order.Status.ToString(),
        PaymentMethod = order.PaymentMethod.ToString(),
        CustomerId = order.CustomerId,
        CustomerName = order.Customer?.FullName,
        CustomerPhone = order.Customer?.Phone,
        CashierUserId = order.CashierUserId,
        Subtotal = order.Subtotal,
        DiscountAmount = order.DiscountAmount,
        TaxAmount = order.TaxAmount,
        TotalAmount = order.TotalAmount,
        Notes = order.Notes,
        CreatedAt = order.CreatedAt,
        PaidAt = order.PaidAt,
        CompletedAt = order.CompletedAt,
        Items = order.Items
            .OrderBy(i => i.ProductName, StringComparer.Ordinal)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .ThenBy(i => i.Id)
            .Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                VariantId = i.VariantId,
                SerialImeiId = i.SerialImeiId,
                Imei = i.SerialImei?.Imei,
                SerialNumber = i.SerialImei?.SerialNumber,
                ProductName = i.ProductName,
                VariantName = i.VariantName,
                Sku = i.Sku,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                DiscountAmount = i.DiscountAmount,
                TotalPrice = i.TotalPrice
            })
            .ToList()
    };
}
