# Shell Navigation & Session Preview (UI-01 Handoff)

## 1. Route & Session Seam

Trong UI-01, chúng tôi đã tách rời hệ thống chuyển trang (Navigation) và phiên làm việc (Session) ra khỏi các lớp giao diện (View).

### `INavigationService` và `IFrameAdapter`
- Navigation API (quản lý lịch sử, kiểm tra quyền truy cập) được trừu tượng hóa bằng `INavigationService`.
- Việc thay đổi nội dung (Content) thực tế trên WinUI `Frame` được thực hiện qua `IFrameAdapter`. 
- Cấu trúc này cho phép kiểm thử toàn bộ luồng chuyển trang, reselect, thao tác Back và giới hạn quyền mà không cần tạo các XAML Controls (headless testing).

### `ICurrentSession`
- Phiên làm việc mặc định trong UI-01 sử dụng `PreviewSessionProvider` (thuộc trạng thái `SessionStatus.Preview`).
- Chế độ này chủ động cung cấp một số capabilities nhất định (`catalog:read`, `inventory:read`...) để mở khóa các menu item nhằm hiển thị giao diện, nhưng **KHÔNG** giả mạo danh tính Admin (không có Token JWT hay profile mapping thực sự).

## 2. Ràng Buộc & Tiền Quyển (Prerequisites)

- Việc lọc menu tại client (`GetAllowedMenuRoutes`) chỉ để cung cấp UX phù hợp; **Client filtering never replaces server RBAC**.
- Giao diện đăng nhập thực thụ và kết nối với Backend Authorization Policy là tiền đề của change **UI-02**. Trước khi UI-02 hoàn tất, Shell hoạt động dưới quyền `Preview`.
- **UI-01 không tạo bất cứ Business Repository nào** (Product/Order/Customer) và hoàn toàn không phụ thuộc vào `ApiClient`. Mọi trang nội dung (ngoài Dashboard ThemePreview) đều dùng chung một view "Chưa Triển Khai".