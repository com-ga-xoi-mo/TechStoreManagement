using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.Api.Orders.Services;

public static class CreateOrderValidator
{
    public const int MaxLines = 100;
    public const int MaxNotesLength = 500;
    public const int MaxQuantity = 10_000;

    public static Dictionary<string, string[]> Validate(CreateOrderRequest? request)
    {
        var errors = new Dictionary<string, List<string>>();

        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                list = [];
                errors[field] = list;
            }
            list.Add(message);
        }

        if (request == null)
        {
            AddError("request", "Request body cannot be null.");
            return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
        }

        if (request.CustomerId.HasValue && request.CustomerId.Value == Guid.Empty)
        {
            AddError("customerId", "Customer ID is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(request.PaymentMethod)
            && !OrderEnumParser.TryParse<PaymentMethod>(request.PaymentMethod, out _))
        {
            AddError("paymentMethod", "Payment method must be one of: Cash, VietQr, Card.");
        }

        if (request.Notes != null && request.Notes.Length > MaxNotesLength)
        {
            AddError("notes", $"Notes cannot exceed {MaxNotesLength} characters.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            AddError("items", "At least one item is required.");
        }
        else if (request.Items.Count > MaxLines)
        {
            AddError("items", $"An order cannot have more than {MaxLines} items.");
        }
        else
        {
            var seenDevices = new HashSet<Guid>();

            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                var prefix = $"items[{i}]";

                if (item == null)
                {
                    AddError(prefix, "Item cannot be null.");
                    continue;
                }

                if (!item.ProductId.HasValue || item.ProductId.Value == Guid.Empty)
                {
                    AddError($"{prefix}.productId", "Product ID is required.");
                }

                if (item.VariantId.HasValue && item.VariantId.Value == Guid.Empty)
                {
                    AddError($"{prefix}.variantId", "Variant ID is invalid.");
                }

                if (item.SerialImeiId.HasValue)
                {
                    if (item.SerialImeiId.Value == Guid.Empty)
                    {
                        AddError($"{prefix}.serialImeiId", "Serial/IMEI ID is invalid.");
                    }
                    else if (!seenDevices.Add(item.SerialImeiId.Value))
                    {
                        AddError($"{prefix}.serialImeiId", "The same device cannot appear more than once in an order.");
                    }
                }

                if (item.Quantity.HasValue)
                {
                    if (item.Quantity.Value < 1)
                    {
                        AddError($"{prefix}.quantity", "Quantity must be at least 1.");
                    }
                    else if (item.Quantity.Value > MaxQuantity)
                    {
                        AddError($"{prefix}.quantity", $"Quantity cannot exceed {MaxQuantity}.");
                    }
                }

                if (item.DiscountAmount.HasValue && item.DiscountAmount.Value < 0)
                {
                    AddError($"{prefix}.discountAmount", "Discount amount cannot be negative.");
                }
            }
        }

        return errors.ToDictionary(k => k.Key, v => v.Value.ToArray());
    }
}
