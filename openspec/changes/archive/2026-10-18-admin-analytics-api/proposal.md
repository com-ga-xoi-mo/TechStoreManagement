# Proposal

## Why

Dashboard preview đã có KPI, doanh thu ngày, tồn thấp và đơn gần nhất nhưng chỉ đọc fixture/unavailable; API hiện chưa có capability Analytics. Dev 6 cần một contract backend có nguồn số liệu, kỳ thời gian, quyền Admin và tính nhất quán được xác định trước khi change Dashboard kết nối dữ liệu thật.

## What Changes

- Thêm một endpoint dashboard tổng hợp cho UC-STAT-01/02/03, chỉ Admin được gọi.
- Công bố DTO dùng chung gồm KPI, khoảng ngày/as-of, chuỗi doanh thu theo ngày, tối đa năm đơn gần nhất trong kỳ, tối đa năm dòng tồn thấp và các comparison nullable.
- Tổng hợp/filter/sort/count ở PostgreSQL qua repository; service điều phối nghiệp vụ và mapping, controller chỉ xử lý HTTP/auth.
- Định nghĩa contract repository nội bộ riêng, không trả API DTO/entity/IQueryable qua ranh giới repository.
- Kiểm chứng business rules, RBAC, snapshot consistency, PostgreSQL aggregation và đối soát trend với tổng revenue.
- Không tích hợp/chỉnh Dashboard UI hoặc mở rộng DataSeeder trong change này.

## Capabilities

### New Capabilities

- `admin-analytics-api`: API thống kê dashboard được giới hạn cho Admin, với time windows, totals, trends và snapshot metadata xác định.

### Modified Capabilities

Không có. `frontend-dashboard-preview` tiếp tục mô tả preview độc lập và cấm báo preview là Analytics integration đã hoàn tất; capability mới này chỉ bổ sung contract API, không đổi hành vi UI.

## Impact

- Backend mới tại module `backend/Analytics/` (repository, internal read contracts, service, controller) và đăng ký DI ở `backend/Program.cs`.
- DTO mới trong `shared/DTOs/`; có thể cần mở rộng `IUnitOfWork`/`UnitOfWork` để service mở transaction `RepeatableRead` mà không tham chiếu EF Core.
- Unit/integration tests trong backend test projects; PostgreSQL là điều kiện cho kiểm chứng aggregate và isolation thực tế.
- Tái sử dụng `Order.CompletedAt`, `CreatedAt`, `TotalAmount`, `Status`; `Product.IsActive`; `InventoryStock.Quantity`, `ReservedQuantity`, `MinStockAlert`; và role claim đã phát hành bởi Auth.
- Không đổi order/stock lifecycle, schema database, Auth implementation, Dashboard client hoặc seed data.

## Source and Spec Reconciliation

- Source xác nhận revenue fields và status `Completed` tồn tại; chuyển order sang Completed set `CompletedAt`. Inventory search hiện lọc tồn thấp bằng `Quantity <= MinStockAlert`, cùng semantics với đề xuất. `AvailableQuantity` hiện là `Quantity - ReservedQuantity` trong `InventoryStockDto`.
- API chưa có Analytics controller/service/repository hay DTO. `AuthController` dùng JWT claims; `StoreController` cho Admin và Manager, nhưng Analytics endpoint sẽ dùng riêng Admin-only role theo phạm vi UC.
- Dashboard fixture model dùng `ProductCount`, `LowStockItem.AvailableQuantity` và demo-specific `RecentOrder` identifiers; đây không phải API contract và sẽ không bị tái sử dụng trực tiếp như API DTO.
- Không thấy xung đột nghiệp vụ giữa đề xuất revenue/order/stock semantics với source hiện tại. Delta tạo capability backend mới; Dashboard preview spec chỉ yêu cầu backend readiness được phân biệt, không bắt change này tích hợp UI.
- Các lựa chọn về gross revenue sau refund, UTC+07 period boundaries, low-stock cap/list semantics, database `asOf`, transaction isolation và internal repository contract được chốt cụ thể trong `design.md`.
