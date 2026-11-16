# Proposal

## Why

M3 Sprint 2 (T. Vinh): "Xóa dữ liệu và hủy request cũ khi đăng xuất hoặc đổi tài khoản; không hiển thị kết quả trả về muộn của phiên trước." Đã có: logout xóa session/file, `App` thay Shell bằng LoginPage, Shell đóng thì lease hủy activation và dispose scope trang, `BearerTokenHandler` chặn request gửi khi phiên đổi **trước** lúc gửi. Còn hở: response về **sau** khi phiên đã đổi vẫn được trả cho caller (ví dụ lệnh "Làm mới" kho dùng `CancellationToken.None`), và route của phiên trước bị dùng lại (xử lý trong `role-based-shell`).

## What Changes

- `BearerTokenHandler` so generation sau khi nhận response; nếu phiên đã đổi thì dispose response và ném lỗi "Phiên đăng nhập đã thay đổi", không trả dữ liệu cho caller.
- Ghi rõ trong spec chuỗi dọn phiên: Shell đóng → hủy activation/dialog → scope trang dispose; route mới theo role (`role-based-shell`).

## Capabilities

### Modified Capabilities

- `frontend-api-access`: response đến muộn không vượt qua ranh giới phiên.

## Impact

- Client: `Services/Api/BearerTokenHandler.cs`; tests `BearerTokenHandlerTests`.
- Không đổi server.
