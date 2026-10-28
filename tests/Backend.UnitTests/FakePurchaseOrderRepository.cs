using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;

namespace TechStore.UnitTests;

public class FakePurchaseOrderRepository : IPurchaseOrderRepository
{
    public List<Supplier> Suppliers { get; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; } = [];

    public Task<Supplier?> FindSupplierByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.FirstOrDefault(s => s.Name == name));

    public void AddSupplier(Supplier supplier) => Suppliers.Add(supplier);

    public void Add(PurchaseOrder purchaseOrder) => PurchaseOrders.Add(purchaseOrder);
}
