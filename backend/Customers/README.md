# Module Customers (Dev 5 phụ trách)

Nội dung triển khai:
- Entities: `Customer.cs`, `LoyaltyPoint.cs`, `Voucher.cs` (Kế thừa từ `BaseEntity`).
- Quản lý thông tin khách hàng, số điện thoại, điểm tích lũy thành viên VIP.
- Fluent API Configurations trong thư mục `Configurations/`.
- Services: `ICustomerService.cs`, `AnalyticsService.cs`.
- Controller: `CustomersController.cs`, `AnalyticsController.cs`.
- Chuẩn bị sẵn bộ dữ liệu mẫu (10 khách hàng mẫu, 20 đơn hàng mẫu nhiều trạng thái) để Dev 6 đưa vào DataSeeder.
