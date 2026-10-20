## Why

Backend Orders API (UC-ORD-01..04) đã merge (PR #7) nhưng màn hình Đơn hàng trên desktop vẫn là placeholder: `OrderListViewModel` rỗng, `OrderListPage.xaml` chỉ ghi "sẽ được kết nối qua API", chưa có màn lập đơn hay xem chi tiết. Sprint 2 yêu cầu Dev 4 làm trọn lát cắt full-stack (MILESTONE_2_PLAN, mục Dev 4) và chuyển trang XAML cũ sang C# Markup; không làm thì luồng E2E của Sprint 3 (lập đơn → trừ kho → hiện trong lịch sử) không chạy được trên giao diện.

## What Changes

- Thêm client repository `IOrderRepository` / `OrderRepository` gọi `POST/GET /api/orders`, `GET /api/orders/{id}`, `PATCH /api/orders/{id}/status`, kiểm tra hình dạng JSON và ánh xạ DTO sang model phía client (theo mẫu `ProductRepository`).
- Thêm repository đọc nhỏ `IOrderDeviceRepository` lấy máy `InStock` của một sản phẩm/biến thể từ `GET /api/inventory/serials`, chỉ giữ id, IMEI, serial (không đưa giá vốn lên client).
- Chuyển `OrderListPage` từ XAML sang C# Markup (`OrderListPage.cs`), xóa `OrderListPage.xaml` và `.xaml.cs`: bảng đơn, lọc trạng thái, tìm theo mã đơn/khách, sắp xếp, phân trang, `StatusBadge` theo trạng thái, nút chuyển trạng thái theo trạng thái hiện tại.
- Thêm `OrderCreatePage.cs` (route mới `OrderCreate` thuộc mục Đơn hàng): chọn sản phẩm và biến thể, chọn IMEI `InStock` cho máy có serial, nhập số lượng và giảm giá dòng, chọn phương thức thanh toán, ghi chú, tổng tiền ước tính theo thời gian thực; hai hành động "Lưu đơn nháp" và "Thanh toán & hoàn tất".
- Thêm `OrderDetailDialog.cs` (chỉ đọc): danh sách dòng hàng kèm IMEI/serial, tổng tiền, các mốc thời gian.
- Bổ sung `PatchAsync` vào `IApiClient` / `ApiClient` (cần cho đổi trạng thái đơn); cập nhật 3 stub `IApiClient` trong test.
- Mở rộng `AppRoute` và `ShellRoutePageFactory` cho route `OrderCreate`; đăng ký DI cho repository, ViewModel, page.
- Test headless: repository, ViewModel (danh sách, lập đơn, đổi trạng thái), `PatchAsync`, route mới.
- Ngoài phạm vi: chọn khách hàng (chưa có API/UI Khách hàng của Dev 5, đơn lập là khách lẻ nhưng request đã hỗ trợ `customerId`), hoàn trả/hoàn tiền, in hóa đơn, voucher, thuế, thanh toán VietQR/thẻ thật, sửa backend Orders.

## Capabilities

### New Capabilities
- `frontend-order-management`: người dùng desktop xem, tìm, lọc, lập đơn, đổi trạng thái và xem chi tiết đơn hàng qua Orders API thật, với trạng thái tải/lỗi/rỗng rõ ràng và dữ liệu tiền, IMEI trung thực.

### Modified Capabilities
- `frontend-api-access`: thêm PATCH vào các thao tác ghi có cùng ràng buộc (cùng origin, không tự retry, lỗi chuẩn hóa).
- `frontend-shell-navigation`: mục Đơn hàng gồm trang danh sách và trang lập đơn; từ lập đơn quay về danh sách mà không đổi mục đang chọn trên thanh điều hướng.

## Impact

- **Mã client mới**: `Repositories/Orders/`, `Models/Orders/`, `ViewModels/Orders/`, `Views/Orders/` (thay `OrderListPage.xaml*`).
- **Mã client sửa**: `Services/Api/IApiClient.cs`, `Services/Api/ApiClient.cs`, `Services/Navigation/AppRoute.cs`, `Views/Shell/ShellRoutePageFactory.cs`, `Views/Shell/ContentDialogPresenter.cs` (chỉ thêm truy cập root cho dialog chi tiết), `UiServiceCollectionExtensions.cs`.
- **Tệp dùng chung có nguy cơ xung đột với Dev 1/2/3/6**: `IApiClient`, `AppRoute`, `ShellRoutePageFactory`, `UiServiceCollectionExtensions`. Thay đổi giữ nhỏ và chỉ thêm, merge theo thứ tự PR.
- **Backend/shared**: không đổi. Dùng `OrderDto`, `OrderItemDto`, `OrderSummaryDto`, `PagedResultDto`, `CreateOrderRequest`, `UpdateOrderStatusRequest`, `SerialImeiDto` hiện có.
- **Phụ thuộc**: PR #12 (Dev 6) đang chuyển `ShellPage`/Dashboard và có thể đụng `Program.cs` client; không phụ thuộc cứng nhưng cần rebase trước khi merge.
- **Tiền đề**: backend phải chạy với dữ liệu seed (4 máy iPhone `InStock`); Orders API hiện chưa yêu cầu đăng nhập nên client chạy được cả khi chưa có token.
