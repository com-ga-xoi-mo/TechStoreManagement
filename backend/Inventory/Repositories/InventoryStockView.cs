using TechStore.Api.Inventory.Entities;

namespace TechStore.Api.Inventory.Repositories;

public sealed record InventoryStockView(InventoryStock Stock, int SerialCount);
