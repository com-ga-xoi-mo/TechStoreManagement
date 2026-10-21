using TechStore.Client.Models.Catalog;

namespace TechStore.Client.Repositories.Catalog;

public interface IProductSpecRepository
{
    /// <summary>GET api/products/spec-keys[?categoryId=]: spec keys and values that exist in stored products.</summary>
    Task<IReadOnlyList<SpecKeyOption>> GetSpecKeysAsync(Guid? categoryId, CancellationToken cancellationToken = default);
}
