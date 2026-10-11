using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface ISerialImeiRepository
{
    Task<(IReadOnlyList<SerialImei> Items, int TotalCount)> SearchAsync(
        SerialImeiSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<ExistingIdentifiersResult> FindExistingIdentifiersAsync(
        IEnumerable<string> serialNumbers,
        IEnumerable<string> imeis,
        CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<SerialImei> items);
}
