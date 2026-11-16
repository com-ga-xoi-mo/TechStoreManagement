# Tasks

## 1. Transport

- [x] 1.1 `BearerTokenHandler`: kiểm generation sau response, dispose + ném lỗi phiên đổi; không áp dụng login/refresh.
- [x] 1.2 Tests: response về sau logout, sau đăng nhập tài khoản khác → lỗi, không trả body; refresh im lặng cùng generation → trả bình thường; login không bị chặn.

## 2. Kiểm thử thủ công (AI)

AI tự chạy, ghi kết quả thật.

- [x] 2.1 Chạy frontend unit tests liên quan session/transport/navigation; ghi Pass/Fail.
- [x] 2.2 Kịch bản headless với `InMemorySessionStore` + `ApiClient` thật trỏ tới API thật: bắt đầu `GET /api/inventory/stocks` bằng Admin qua handler có độ trễ, logout + login `cashier1` trước khi response về → caller nhận lỗi phiên đổi.
- [x] 2.3 Chạy app Skia với API thật: Admin mở Danh mục và Kho → đăng xuất → đăng nhập `cashier1`; ghi log route/landing, xác nhận không có request nào của phiên Admin được ghi nhận sau khi đổi phiên.

## Kết quả kiểm thử (AI, 2026-11-17)

- 2.1 Filter `FullyQualifiedName~Session|FullyQualifiedName~Transport|FullyQualifiedName~Navigation`: frontend **163 Pass / 0 Fail / 0 Skipped**, `/tmp/m3-session-tests.log`. Lệnh đã chạy trên solution thay vì chỉ project frontend nên có thêm backend 7 Pass / 2 Skipped; không tính skip là đạt.
- Build desktop **Pass**, 0 warning/error (`/tmp/m3-session-build.log`).
- 2.2 `dotnet run --project /tmp/m3-live-runner -- session`: client production thật (`InMemorySessionStore`, `BearerTokenHandler`, `ApiClient`) gọi API/DB thật trên 5199. Admin generation 1: control GET thành công; giữ response inventory sau HTTP, xóa phiên cục bộ + login Sales generation 3 rồi thả response: caller nhận lỗi đổi phiên, dữ liệu cũ bị loại. **Pass**. Log `/tmp/m3-session-live.log`; đây là logout cục bộ, không tuyên bố gọi API logout/revoke.
- 2.3 Chạy Skia trỏ API thật, timeout 15 giây (124), log `/tmp/m3-session-skia.log` có cảnh báo Uno dev-server/X11. **Chưa kiểm tra** luồng click Admin → logout → Sales trên UI và log request theo UI vì không điều khiển/quan sát được Wayland; giữ checkbox mở. Headless 2.2 không thay thế nghiệm thu UI.

- 2.3 (17/11): Kiểm bằng client production (`InMemorySessionStore`, `BearerTokenHandler`, `ApiClient`, `NavigationService`, `RoleAccess`) + API thật, không bấm trên cửa sổ Skia: Admin ở Customers → logout → `cashier1` mở Orders; response kho Admin về muộn bị chặn; token cũ 401. **Pass**
