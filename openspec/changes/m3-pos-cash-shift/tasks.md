## 1. Contract, schema and migration

- [x] 1.1 Thêm `PosShiftDto`, `PosShiftSummaryDto`, `OpenShiftRequest`, `CloseShiftRequest`, `PosCheckoutRequest` trong `shared/`; thêm `PosSessionId`, `CashTendered`, `ChangeGiven` vào `OrderDto`
- [x] 1.2 Entity/configuration: `orders.cash_tendered`, `change_given`, `checkout_key` (+ unique index một phần), `pos_sessions.expected_closing_balance`, `closing_note`, `closed_by_user_id`, FK `orders.pos_session_id`, `pos_sessions.closed_by_user_id` + index (partial unique index `uq_pos_sessions_cashier_open` đã có từ InitialCreate), check `cash_tendered >= 0`, `change_given >= 0`
- [x] 1.3 Migration `AddPosCashShift` và cập nhật `docs/database/schema.dbml`; test migration trên PostgreSQL
- [x] 1.4 Seed ca demo giữ nguyên: các cột mới nullable nên không cần đổi dữ liệu đã seed (test seed hiện có vẫn đạt)

## 2. Orders: tách bước dựng đơn

- [x] 2.1 Thêm `IOrderService.PrepareAsync` (validate + định giá, trả đơn chưa lưu) và để `CreateAsync` dùng lại; bộ test `OrderServiceTests.Create` hiện có phải vẫn đạt
- [x] 2.2 Map các trường POS mới trong `MapToDto`; test đơn nháp trả null ở các trường đó

## 3. Sales: ca thu ngân

- [x] 3.1 `IPosSessionRepository`/`PosSessionRepository`: lấy ca Open của người dùng, `GetForUpdateAsync`, tìm theo bộ lọc có phân trang (`AsNoTracking`)
- [x] 3.2 `IPosShiftService`: mở ca (kiểm 1 ca Open, ánh xạ `23505` sang 409), ca hiện tại, danh sách theo quyền, chi tiết, đóng ca với đối soát và note bắt buộc khi chênh lệch
- [x] 3.3 Test đơn vị với fake repository: mở lần hai, số âm/quá 2 chữ số thập phân, đóng đủ/thiếu/thừa, thiếu note, ca người khác (Sales 404, Manager đóng được), đóng hai lần

## 4. Sales: checkout tiền mặt

- [x] 4.1 `IPosCheckoutService` theo thứ tự D2: kiểm tra đầu vào, khóa ca, idempotency, reserve + confirm sale, ghi tiền đơn/ca, commit; rollback khi lỗi
- [x] 4.2 Ánh xạ lỗi: 400 (validate, tiền thiếu), 404 (ca không phải của mình), 409 (ca đã đóng, kho, trùng IMEI, đua)
- [x] 4.3 Test đơn vị: thành công, tiền thừa, thiếu tiền, giá client bị bỏ qua, ca đóng/ca người khác, IMEI trùng, đơn rỗng, số lượng 0/âm/quá tồn, giảm giá vượt, gửi lại cùng khóa, lỗi giữa chừng không để lại gì (`FakeUnitOfWork` đếm rollback)
- [x] 4.4 Test PostgreSQL thật: hai request cùng `checkoutKey` song song; hai đơn tranh IMEI cuối với điểm đồng bộ; bán trong lúc đóng ca; tiền ca khớp tổng đơn; sửa mô tả ca "race" tuần tự trong `OrdersApiIntegrationTests` như kế hoạch M3

## 5. API, quyền và tài liệu backend

- [x] 5.1 `PosController` với các route trong proposal, actor từ token, ProblemDetails 400/404/409; đăng ký DI trong `Program.cs`/extension
- [x] 5.2 Cập nhật `docs/API_PERMISSIONS.md` và baseline `ApiAuthorizationCoverageTests`; test HTTP 401/403 cho từng route `/api/pos`
- [x] 5.3 Cập nhật `backend/Sales/README.md` (thứ tự khóa, luồng checkout, ca) và `docs/MILESTONE_3_PLAN.md` (đánh dấu phần Đạt đã có bằng chứng)

## 6. Client

- [x] 6.1 `IPosRepository`/`PosRepository` và model client (ca, giỏ, kết quả checkout); test repository (dựng request, ánh xạ lỗi)
- [x] 6.2 `PosViewModel`: ca hiện tại, quét/thêm dòng, chặn IMEI trùng trong giỏ, đổi số lượng, chọn khách/khách lẻ, nhập tiền, tiền thừa, Pay không bấm lặp, giữ `checkoutKey` khi thử lại; test headless
- [x] 6.3 Mở/đóng ca nằm trong `PosViewModel` (không tách `ShiftDialogViewModel`): note bắt buộc khi chênh lệch, hiển thị chênh lệch; test headless
- [x] 6.4 `PosPage` và dialog mở/đóng ca bằng C# Markup (token theme, binding đúng đường dẫn, kiểm tra chạy thật), route/menu cho `SalesStaff`/`Admin`, đăng ký DI
- [x] 6.5 `OrderDetailDialog` hiển thị tiền khách đưa và tiền thừa; cập nhật bảng trạng thái chuyển đổi trong AGENTS/README nếu có

## 7. Kiểm chứng

- [x] 7.1 `dotnet test` backend và frontend không tăng số fail/skipped; build `net10.0-desktop`
- [ ] 7.2 Chạy luồng thật mở ca → bán → đóng ca → xem đơn: phần API/PostgreSQL đã đạt qua `PosCheckoutPostgresHttpTests` (8 test, gồm đồng thời); phần UI desktop chưa chạy được (Codex bị lỗi sandbox, chưa kiểm Light/Dark và binding) nên cần chạy tay trước khi đóng change
