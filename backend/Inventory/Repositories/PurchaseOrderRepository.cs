using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public class PurchaseOrderRepository(AppDbContext dbContext) : IPurchaseOrderRepository
{
    public Task<Supplier?> FindSupplierByNameAsync(string name, CancellationToken cancellationToken = default) =>
        dbContext.Suppliers.FirstOrDefaultAsync(s => s.Name == name, cancellationToken);

    public void AddSupplier(Supplier supplier) => dbContext.Suppliers.Add(supplier);

    public void Add(PurchaseOrder purchaseOrder) => dbContext.PurchaseOrders.Add(purchaseOrder);
}
