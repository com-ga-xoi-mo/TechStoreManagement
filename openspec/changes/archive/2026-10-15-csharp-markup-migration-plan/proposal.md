# Proposal: C# Markup Migration Plan & Documentation Update

## Why

Nhóm phát triển quyết định chuyển đổi toàn bộ tầng giao diện Frontend (Uno Platform / WinUI 3) từ XAML sang C# Markup (`Uno.Extensions.Markup`) nhằm đạt được tính an toàn kiểu dữ liệu 100% (compile-time type safety), refactoring dễ dàng và loại bỏ ma sát khi debug XAML. 

Để toàn bộ 6 kỹ sư trong nhóm triển khai đồng bộ và nhất quán ngay trong Sprint 2 của Milestone 2, cần cập nhật toàn diện tài liệu kiến trúc (`AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/TECH_REPORT.md`), đặc tả kế hoạch Sprint 2 (`docs/MILESTONE_2_PLAN.md`) với các đầu việc chuyển đổi và phát triển UI bằng C# Markup cụ thể cho từng thành viên, đồng thời cập nhật các spec Frontend liên quan trong OpenSpec.

## What Changes

- **Cập nhật quy chuẩn kỹ thuật Frontend trong `AGENTS.md`**:
  - Chuyển đổi định nghĩa tầng View từ `View (XAML)` sang `View (C# Markup)`.
  - Thay thế quy ước XAML templates, Resource Dictionaries (`.xaml`) bằng C# Markup extensions, C# ThemeResources/Styles.
  - Cập nhật hướng dẫn và cấu trúc thư mục UI cho client.
- **Cập nhật Kế hoạch Sprint 2 trong `docs/MILESTONE_2_PLAN.md`**:
  - Bổ sung task setup hạ tầng C# Markup (`Uno.Extensions.Markup`) và port Base Styles/Controls (Dev 6 & Dev 1 Lead).
  - Cập nhật chi tiết nhiệm vụ Sprint 2 của cả 6 kỹ sư: xây dựng UI màn hình chức năng hoàn toàn bằng C# Markup thay cho XAML:
    - **Dev 1 (Lead)**: `LoginPage`, `SettingsPage`, `ShellPage` (C# Markup).
    - **Dev 2**: `ProductListPage`, `ProductDetailPage` / `ProductDetailDialog` và Dynamic JSONB Specs Editor (C# Markup).
    - **Dev 3**: `CategoryListPage` (Cây danh mục), `InventoryPage` & IMEI Management Dialog (C# Markup).
    - **Dev 4**: `OrderCreatePage` (Form POS chọn SP, IMEI, tính tiền), `OrderListPage`, `OrderDetailDialog` (C# Markup).
    - **Dev 5**: `CustomerListPage`, `CustomerDetailDialog` (C# Markup) và giám sát chuẩn hóa UI Design Tokens C# Markup.
    - **Dev 6**: `DashboardPage`, `StatCard`, Charts, base reusable controls & theme definitions bằng C# Markup.
- **Cập nhật tài liệu kiến trúc chung (`docs/ARCHITECTURE.md`, `docs/TECH_REPORT.md`)**:
  - Cập nhật sơ đồ kiến trúc MVVM: View (C# Markup) thay cho View (XAML).
  - Cập nhật cây thư mục và công nghệ Client sang Uno Platform C# Markup.
- **Cập nhật các đặc tả OpenSpec Frontend**:
  - Ghi nhận việc sử dụng C# Markup cho design system, shared controls và shell navigation.

## Capabilities

### New Capabilities
<!-- Không có capability nghiệp vụ mới nào được tạo -->

### Modified Capabilities
- `frontend-design-system`: Cập nhật yêu cầu và kịch bản biểu diễn Design Tokens, typography, bảng màu và semantic resources thông qua C# Markup ThemeResources/Markup Extensions thay cho XAML ResourceDictionaries.
- `frontend-shared-controls`: Cập nhật hình thức hiện thực và composition của các reusable controls (`StatCard`, `SearchBoxControl`, `EmptyStateControl`, `StatusBadge`) sang mô hình code-first C# Markup.
- `frontend-shell-navigation`: Cập nhật cấu trúc khai báo View, Shell NavigationView và page container sử dụng C# Markup Fluent API kết hợp MVVM `{x:Bind}` qua C# Markup extensions.

## Impact

- **Tài liệu & Kế hoạch**: `AGENTS.md`, `docs/MILESTONE_2_PLAN.md`, `docs/ARCHITECTURE.md`, `docs/TECH_REPORT.md`.
- **OpenSpec Specifications**: Delta specs cho `frontend-design-system`, `frontend-shared-controls`, `frontend-shell-navigation`.
- **Dependencies (Frontend)**: Cần bổ sung package `Uno.Extensions.Markup` / `Uno.WinUI.Markup` trong client project khi triển khai.
- **Khả năng tương thích**: Không làm ảnh hưởng đến tầng Backend API, Database, DTO contracts hay tầng ViewModel/Repository hiện hành; 100% logic nghiệp vụ MVVM và headless tests được bảo toàn.
