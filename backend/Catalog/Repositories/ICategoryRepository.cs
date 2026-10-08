// Note: This repository is owned and extended by Dev 3 (Categories & Inventory).
// Dev 2 creates this minimal contract so Catalog/Products can look up categories through a repository.

using TechStore.Api.Catalog.Entities;

namespace TechStore.Api.Catalog.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
