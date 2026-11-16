# Tasks

## 1. Transport

- [ ] 1.1 `BearerTokenHandler`: kiểm generation sau response, dispose + ném lỗi phiên đổi; không áp dụng login/refresh.
- [ ] 1.2 Tests: response về sau logout, sau đăng nhập tài khoản khác → lỗi, không trả body; refresh im lặng cùng generation → trả bình thường; login không bị chặn.

## 2. Kiểm thử thủ công (AI)

AI tự chạy, ghi kết quả thật.

- [ ] 2.1 Chạy frontend unit tests liên quan session/transport/navigation; ghi Pass/Fail.
- [ ] 2.2 Kịch bản headless với `InMemorySessionStore` + `ApiClient` thật trỏ tới API thật: bắt đầu `GET /api/inventory/stocks` bằng Admin qua handler có độ trễ, logout + login `cashier1` trước khi response về → caller nhận lỗi phiên đổi.
- [ ] 2.3 Chạy app Skia với API thật: Admin mở Danh mục và Kho → đăng xuất → đăng nhập `cashier1`; ghi log route/landing, xác nhận không có request nào của phiên Admin được ghi nhận sau khi đổi phiên.
