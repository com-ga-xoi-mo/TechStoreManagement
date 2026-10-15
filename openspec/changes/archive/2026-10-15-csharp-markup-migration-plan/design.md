# Design: C# Markup Migration & Documentation Realignment

## Context

Dự án TechStore đang xây dựng phân hệ Quản trị (Milestone 2) trên nền tảng Uno Platform 6.7 (.NET 10). Ban đầu, tầng View được hoạch định triển khai bằng XAML. Nhóm đã quyết định chuyển dịch 100% tầng giao diện người dùng sang C# Markup (`Uno.Extensions.Markup`). 
Mục tiêu của change này là hoàn thiện tài liệu hướng dẫn nhóm (`AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/TECH_REPORT.md`) và phân rã các đầu việc chi tiết trong `docs/MILESTONE_2_PLAN.md` cho từng thành viên trong Sprint 2 mà không làm gián đoạn hay sửa đổi logic ở tầng ViewModel, Model, Repositories, hay Backend API.

## Goals / Non-Goals

**Goals:**
- Chuẩn hóa toàn bộ tài liệu kiến trúc kỹ thuật để ghi nhận C# Markup là chuẩn duy nhất của Client UI.
- Cập nhật quy ước lập trình, cấu trúc thư mục UI và hướng dẫn data binding cho C# Markup trong `AGENTS.md`.
- Bổ sung kế hoạch hành động và ma trận trách nhiệm Sprint 2 trong `docs/MILESTONE_2_PLAN.md` cho 6 kỹ sư, phân bổ rõ ràng các thành phần C# Markup:
  - Dev 1 Lead: Foundation Markup, `LoginPage`, `SettingsPage`, `ShellPage`.
  - Dev 2: `ProductListPage`, `ProductDetailDialog` / specs editor C# Markup.
  - Dev 3: `CategoryListPage`, `InventoryPage`, IMEI manager C# Markup.
  - Dev 4: `OrderCreatePage`, `OrderListPage`, `OrderDetailDialog` C# Markup.
  - Dev 5: `CustomerListPage`, `CustomerDetailDialog`, đồng bộ Design Tokens C# Markup.
  - Dev 6: `DashboardPage`, Base Controls (`StatCard`, `SearchBoxControl`, etc.), Theme resources C# Markup.

**Non-Goals:**
- Sửa code runtime hoặc xóa các file XAML hiện tại ngay trong change này (việc code và xóa XAML sẽ được thực hiện khi bắt đầu Sprint 2 qua apply workflow của các feature slice).
- Thay đổi kiến trúc MVVM, Client Repository, Backend Web API hoặc DTOs.

## Decisions

### 1. Giữ nguyên MVVM + Client Repository, chỉ thay đổi tầng View
- **Quyết định:** ViewModel tiếp tục kế thừa `PageViewModel` và sử dụng `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). Tầng View chuyển từ `.xaml` sang class C# thuần (`.cs`) sử dụng C# Markup extensions.
- **Lý do:** Giữ vững nguyên tắc phân tách mối quan tâm (SoC), tận dụng 100% tầng logic và headless tests đã viết trong `tests/Frontend.UnitTests`.

### 2. Tổ chức Design Tokens và C# Theme Resources
- **Quyết định:** Chuyển đổi các Resource Dictionaries (`Colors.xaml`, `Typography.xaml`, `ControlStyles.xaml`) thành các class C# tĩnh hoặc ThemeDictionary extensions (ví dụ: `AppColors.cs`, `AppTypography.cs`).
- **Lý do:** Cho phép autocomplete Intellisense ngay trong C# Markup code mà không sợ gõ sai key chuỗi tài nguyên tĩnh.

### 3. Phân bổ công việc Sprint 2 cho 6 thành viên
- **Quyết định:** 
  - Khởi đầu Sprint 2: Dev 1 và Dev 6 dựng xong C# Markup package setup, C# theme tokens và Shared Controls.
  - Sau đó: Mỗi thành viên (Dev 1..6) tự chịu trách nhiệm dựng View C# Markup cho màn hình thuộc phân hệ của mình, kết nối trực tiếp với ViewModel tương ứng.

## Risks / Trade-offs

- **[Risk]** Độ dốc học tập cú pháp C# Markup của một số thành viên trong nhóm → **[Mitigation]** Cung cấp mẫu code chuẩn (reference sample C# Markup) trong `AGENTS.md` và Component Gallery.
- **[Risk]** Xung đột khi xóa file XAML cũ → **[Mitigation]** Migration theo từng page độc lập trong Sprint 2: màn hình nào chuyển sang C# Markup thì xóa file `.xaml` và `.xaml.cs` tương ứng.
