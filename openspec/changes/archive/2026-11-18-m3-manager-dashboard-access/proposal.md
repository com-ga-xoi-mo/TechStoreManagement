# Proposal

## Why

M3 Sprint 2 giao Bình mở Dashboard live cho Manager. API và Shell hiện chỉ cho Admin vào Tổng quan, còn các kỳ dựa trên ngày hiện tại khiến lịch sử đơn demo quanh 18/10/2026 có thể nằm ngoài kỳ khi trình diễn.

## What Changes

- Cấp quyền đọc Dashboard toàn cửa hàng cho Admin và Manager. Đồng bộ API, bảng quyền, menu và route guard. Sau đăng nhập, Manager vào Tổng quan theo thứ tự điều hướng hiện có.
- Thêm kỳ tùy chọn `period=custom` với hai ngày Việt Nam `from` và `to`, tối đa 31 ngày, trên endpoint và Dashboard live. Giữ ba kỳ hiện có và mặc định `last7Days`. Ví dụ nghiệm thu demo chọn 12–18/10/2026 trên database đã seed.
- Dùng lại `AdminDashboardDto` và định nghĩa KPI. Kỳ tùy chọn lọc doanh thu và đơn hàng theo ngày báo cáo. KPI sản phẩm hoạt động và tồn thấp tiếp tục phản ánh thời điểm đọc. Preview tiếp tục được bật riêng bằng cấu hình.
- Bàn giao bằng chứng đăng nhập `manager1`, đọc Dashboard live và kiểm tra HTTP của bốn role. Đối soát đầy đủ với bộ số liệu tính tay thuộc `m3-demo-data-reconciliation`.

## Capabilities

### New Capabilities

Không có capability mới.

### Modified Capabilities

- `admin-analytics-api`: Quyền truy cập, contract khoảng ngày và biên kỳ Việt Nam.
- `api-role-authorization`: Quyền Dashboard trong ma trận API và kịch bản kiểm tra role.
- `frontend-dashboard-preview`: Dashboard live cho Manager, chọn khoảng ngày, refresh/retry và nguồn dữ liệu preview.
- `frontend-shell-navigation`: Menu, landing page và guard Tổng quan theo role.

## Impact

- Backend Analytics, bộ kiểm kê quyền API, `docs/API_PERMISSIONS.md` và mô tả Analytics trên Swagger.
- Shared DTO giữ tên và JSON shape, mở rộng giá trị hợp lệ của `period`. Desktop cập nhật `RoleAccess`, các model/repository Analytics, `DashboardViewModel` và `DashboardPage`.
- Phạm vi refactor màu biểu đồ `StitchTokens`, quyền Customers và dữ liệu seed nằm ngoài change. Apply dùng database test đã seed để kiểm chứng đường đọc live.
