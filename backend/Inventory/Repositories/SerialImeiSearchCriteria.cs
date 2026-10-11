using TechStore.Shared.Enums;

namespace TechStore.Api.Inventory.Repositories;

public sealed record SerialImeiSearchCriteria(
    Guid? ProductId,
    Guid? VariantId,
    SerialImeiStatus? Status,
    int Page,
    int PageSize);

public sealed record ExistingIdentifiersResult(
    IReadOnlyList<string> ExistingSerials,
    IReadOnlyList<string> ExistingImeis)
{
    public bool HasAnyDuplicates => ExistingSerials.Count > 0 || ExistingImeis.Count > 0;
}
