# Module Inventory (Dev 3 phụ trách)

Nội dung triển khai:
- Entities: `InventoryStock.cs`, `SerialImei.cs`, `PurchaseOrder.cs`, `Supplier.cs` (Kế thừa từ `BaseEntity`).
- Quản lý mã IMEI duy nhất cho từng máy giá trị cao (trạng thái: InStock, Sold, UnderWarranty).
- Fluent API Configurations trong thư mục `Configurations/`.
- Services: `IInventoryService.cs`, `InventoryService.cs` (Transaction & Khóa Concurrency `FOR UPDATE`).
- Controller: `InventoryController.cs`.
