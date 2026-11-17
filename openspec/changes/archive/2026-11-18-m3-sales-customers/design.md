## Context

`CustomersController` đặt `[Authorize(Roles = "Admin")]` ở mức lớp, nên SalesStaff và Manager đều nhận 403. `CustomerService` không biết ai đang gọi. Trang tạo đơn (`OrderCreatePage`) chưa chọn được khách; POS của Đạt (nhánh `feat/m3-pos-cash-shift`, chưa merge) cũng chưa có. Client đã có `ICustomerRepository`, `CustomerFormViewModel` và form tạo khách.

## Goals / Non-Goals

**Goals:**
- Sales tìm, chọn, tạo khách tại quầy; Manager quản lý khách như Admin; Warehouse không đọc khách.
- Quyền kiểm ở server theo từng action, client chỉ ẩn nút.
- Một bộ chọn khách dùng chung cho trang tạo đơn và POS.

**Non-Goals:**
- Điểm thưởng, hạng VIP, voucher (M4 của Vy).
- Sửa luồng POS/checkout của Đạt; chỉ bàn giao bộ chọn.
- Đổi DTO hoặc schema Customers.

## Decisions

**D1. Bảng quyền.** Đọc + tạo: A M S. Sửa, vô hiệu hóa/kích hoạt, lịch sử đơn/thiết bị/điểm: A M. W: không gì.
- Sales cần tạo khách ngay ở quầy, nhưng không được sửa số điện thoại hoặc thông tin khách: số điện thoại là khóa tra cứu và sẽ gắn với điểm/voucher ở M4, đổi tại quầy dễ chuyển nhầm quyền lợi sang người khác. Sửa thông tin là việc của Manager/Admin.
- Lịch sử đơn của khách gồm đơn do thu ngân khác bán. Bảng quyền Orders chỉ cho Sales xem đơn của mình, nên mở lịch sử khách cho Sales sẽ vòng qua giới hạn đó. Vì vậy lịch sử chỉ cho A M.
- Hồ sơ và danh sách vẫn trả `orderCount`/`totalSpent`/`lastPurchaseAt` cho Sales: đây là số liệu của riêng một khách, không phải doanh thu cửa hàng, và giúp Sales nhận ra khách quen. Không thêm `FieldAccess` mới.
- Thay thế đã cân nhắc: cho Sales sửa liên hệ trừ số điện thoại. Bỏ vì thêm một hình dạng request riêng mà M3 chưa cần.

**D2. Sales chỉ thấy khách đang hoạt động, ép tại server.** `SearchAsync(request, role)` ghi đè `IsActive = true` khi role là SalesStaff; `GetByIdAsync(id, role)` trả NotFound cho khách không hoạt động. Khách bị vô hiệu hóa không bán được (OrderService đã chặn), nên Sales không cần thấy họ; trả 404 thay vì 403 để không lộ sự tồn tại. Service nhận `RoleType` thay vì cả `AuthenticatedUser` vì chỉ cần role, giống `IProductService.GetByIdAsync(id, includeCost)`.

**D3. Policy theo action.** Lớp giữ `[Authorize(Roles = "Admin,Manager,SalesStaff")]`, các action sửa/lịch sử thêm `[Authorize(Roles = "Admin,Manager")]` (ASP.NET Core yêu cầu cả hai đều đạt). Action cần role đọc bằng `AuthenticatedUser.TryRead`, thiếu claim trả 401 như `OrdersController`. Baseline `ApiAuthorizationCoverageTests` cập nhật cùng lúc.

**D4. Client.** `RoleAccess.CanOpen(Customers)` = A M S; thêm `CanCreateCustomers` (A M S) và `CanManageCustomers` (A M). `CustomerListViewModel` và `CustomerDetailViewModel` nhận `ISessionStore?` tùy chọn (giống `OrderListViewModel`), lộ `CanManageCustomers`; View bind `Visibility` theo cờ này. `CustomerDetailViewModel` không gọi API lịch sử khi không có quyền. Với Sales, bộ lọc trạng thái ẩn và luôn gửi `isActive=true`.

**D5. Bộ chọn khách.** `ViewModels/Customers/CustomerPickerViewModel` (ObservableObject, không phải PageViewModel vì nằm trong trang khác) dùng `ICustomerRepository.SearchAsync` với `IsActive = true`, `PageSize = 8`, debounce 300 ms và bỏ kết quả cũ bằng số thứ tự request. Tạo khách dùng lại `CustomerFormViewModel`; khi lưu xong picker chọn khách mới. 409 → thông báo và tìm theo số đó. `Views/Customers/CustomerPickerView` là `UserControl` C# Markup. Trang tạo đơn nhận picker qua DI (tham số tùy chọn để test cũ không đổi) và đưa `SelectedCustomerId` vào `CreateOrderRequest.CustomerId`. Đạt dùng cùng hai lớp này cho POS.

**D6. Thứ tự tìm và chọn khách.** Theo yêu cầu Vy, nút Khách mới nằm ở góc phải cùng hàng với tiêu đề Khách hàng; ô tìm nằm ngay bên dưới. Trạng thái và kết quả tìm nằm dưới ô tìm. Chỉ khi đã chọn khách mới hiện thông tin khách và nút Bỏ chọn bên dưới kết quả; chưa chọn hoặc đã bỏ chọn thì ẩn cả phần thông tin, không hiện Khách lẻ/Không tích điểm. Không chọn khách vẫn gửi đơn không có customerId.

## Risks / Trade-offs

- [Đạt và Bình cùng sửa bảng quyền/spec `api-role-authorization`, `frontend-shell-navigation`] → mỗi người chỉ sửa dòng của mình; An merge theo thứ tự, người merge sau rebase lại bảng.
- [Sales tạo khách trùng do gõ sai số] → unique index `idx_customers_phone` vẫn trả 409; picker chuyển sang tìm số đó.
- [Client ẩn nút nhưng route vẫn mở] → server trả 403 cho mọi action ngoài quyền; test HTTP kiểm từng action.

## Migration Plan

Không có migration. Rollback: revert commit; dữ liệu không đổi.
