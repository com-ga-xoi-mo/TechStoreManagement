# Module Identity & Seeding (Dev 6 phụ trách)

Nội dung triển khai:
- Entities: `User.cs`, `Role.cs`, `RefreshToken.cs` (Kế thừa từ `BaseEntity`).
- Enum `RoleType`: Admin, Manager, SalesStaff, WarehouseStaff trong `shared/Enums/`.
- Fluent API Configurations trong thư mục `Configurations/`.
- Services: `IAuthService.cs`, `JwtService.cs`, `GeminiAiAgentService.cs`.
- Controller: `AuthController.cs`, `AiController.cs`.
- Data Seeder: Viết `backend/Common/Data/DataSeeder.cs` tự động nạp >= 20 sản phẩm công nghệ (kèm specs), >= 5 danh mục, >= 10 khách hàng, >= 20 đơn hàng mẫu và 4 tài khoản demo (admin, manager, staff, warehouse).
