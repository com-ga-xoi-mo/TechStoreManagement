# Design

## Context
`BearerTokenHandler` lấy `generation` trước khi refresh/gửi; nếu đổi trước khi gửi thì ném `InvalidOperationException`. Sau `base.SendAsync` chỉ xử lý 401.

## Decisions

### D1. Kiểm generation hai lần, cùng thông báo lỗi
Sau khi nhận response, nếu `sessionStore.Current.Generation != generation` (request không phải login/refresh) thì `response.Dispose()` và ném cùng `InvalidOperationException` như kiểm tra trước khi gửi. Mọi caller đã xử lý lỗi transport kiểu này; trang thuộc phiên cũ đã bị dispose nên không hiển thị gì. Một chỗ sửa áp dụng cho mọi repository.

### D2. Không làm cơ chế xóa cache mới
Dữ liệu trang chỉ sống trong ViewModel/scope của route; singleton không giữ dữ liệu nghiệp vụ (đã soát `CategoryDialogRequests`, `OrderDetailDialogPresenter`, `ContentDialogPresenter`). Theme là tùy chọn của máy, không phải dữ liệu tài khoản.

## Non-Goals
Không hủy ngược request ghi đã tới server trước khi đổi phiên (server đã xử lý theo token lúc đó).
