# Tasks

## 1. Cập nhật Quy chuẩn Kỹ thuật Repository (AGENTS.md)

- [x] 1.1 Cập nhật tài liệu `AGENTS.md` chuyển đổi toàn bộ quy ước giao diện từ XAML sang Uno Platform C# Markup (`Uno.Extensions.Markup`), bao gồm mẫu cú pháp data binding `{x:Bind}` qua code-first C#, quy ước loại bỏ code-behind, và quy tắc cấu trúc thư mục Client UI.
- [x] 1.2 Cập nhật mô tả kiến trúc Frontend trong `AGENTS.md` phản ánh rõ ràng mô hình MVVM + Client Repository kết hợp View (C# Markup).

## 2. Cập nhật Kế hoạch Triển khai Sprint 2 (docs/MILESTONE_2_PLAN.md)

- [x] 2.1 Cập nhật tổng quan Sprint 2 trong `docs/MILESTONE_2_PLAN.md`: ghi nhận quyết định chiến lược chuyển đổi 100% UI sang C# Markup và điều kiện tiên quyết kích hoạt C# Markup packages.
- [x] 2.2 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 1 (Lead)**: Thiết lập cấu hình Uno C# Markup, xây dựng `LoginPage.cs`, `SettingsPage.cs` và `ShellPage.cs` bằng C# Markup.
- [x] 2.3 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 2**: Xây dựng `ProductListPage.cs`, `ProductDetailPage.cs` / `ProductDetailDialog.cs` và bộ soạn thảo JSONB Hardware Specs bằng C# Markup.
- [x] 2.4 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 3**: Xây dựng `CategoryListPage.cs` (Cây danh mục), `InventoryPage.cs` và dialog quản lý Serial/IMEI bằng C# Markup.
- [x] 2.5 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 4**: Xây dựng `OrderCreatePage.cs` (Form POS chọn khách, SP, IMEI, realtime calculation), `OrderListPage.cs`, `OrderDetailDialog.cs` bằng C# Markup.
- [x] 2.6 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 5**: Xây dựng `CustomerListPage.cs`, `CustomerDetailDialog.cs` bằng C# Markup; điều phối Design Tokens C# Markup và đảm bảo trải nghiệm người dùng nhất quán.
- [x] 2.7 Bổ sung chi tiết các đầu việc Sprint 2 cho **Dev 6**: Xây dựng `DashboardPage.cs`, các Base Controls dùng chung (`StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge`) và Theme/Colors bằng C# Markup.

## 3. Cập nhật Tài liệu Kiến trúc Chung (docs/ARCHITECTURE.md & docs/TECH_REPORT.md)

- [x] 3.1 Cập nhật sơ đồ và giải thích MVVM trong `docs/ARCHITECTURE.md`: chuyển `VIEW (XAML)` thành `VIEW (C# Markup)`.
- [x] 3.2 Cập nhật mô tả stack công nghệ Client trong `docs/TECH_REPORT.md`: ghi nhận việc dùng Uno Platform C# Markup thay cho XAML thuần.

## 4. Xác minh và Kiểm tra Đồng bộ

- [x] 4.1 Chạy lệnh `openspec validate csharp-markup-migration-plan --strict` để đảm bảo toàn bộ spec deltas và artifacts tuân thủ 100% schema và không có lỗi cú pháp.
- [x] 4.2 Kiểm tra đối soát nội dung giữa `AGENTS.md`, `docs/MILESTONE_2_PLAN.md` và các tài liệu liên quan nhằm đảm bảo phân công vai trò 6 kỹ sư khớp hoàn toàn với kiến trúc C# Markup.
