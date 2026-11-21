using System.Text.Json.Serialization;

namespace TechStore.Shared.DTOs;

public sealed record PosQuoteDto(
    [property: JsonRequired] decimal Subtotal,
    [property: JsonRequired] decimal DiscountAmount,
    [property: JsonRequired] decimal TaxAmount,
    [property: JsonRequired] decimal TotalAmount);
