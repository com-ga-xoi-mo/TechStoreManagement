using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public interface IPurchaseOrderRepository
{
    Task<Supplier?> FindSupplierByNameAsync(string name, CancellationToken cancellationToken = default);

    void AddSupplier(Supplier supplier);

    // Stages the purchase order together with the lines in its Items collection.
    void Add(PurchaseOrder purchaseOrder);
}
