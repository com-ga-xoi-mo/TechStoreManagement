## Context

Hiện trạng: `OrderService.CreateAsync` tự mở transaction ngắn để lưu đơn `Pending`; `UpdateStatusAsync` khóa đơn, gọi `IInventoryFulfillmentService.ReserveAsync` rồi `ConfirmSaleAsync` và ghi `PaidAt`/`CompletedAt`. `PosSession` đã có entity và cấu hình nhưng không có service, controller hay index "một ca Open mỗi người". `OrderPricingHelper` (Vy) trả thuế và giảm giá cấp đơn bằng 0. `IUnitOfWork` không cho transaction lồng nhau, nên checkout một transaction không thể gọi `CreateAsync` rồi `UpdateStatusAsync`.

Ràng buộc: `Controller → Service → Repository → AppDbContext`; repository không save; khóa dòng theo thứ tự cố định; tiền `numeric(18,2)`, thời gian UTC; An ghép migration nên migration của change này phải tách riêng, thêm cột, không sửa migration cũ.

## Goals / Non-Goals

**Goals:**
- Bán tiền mặt một request, một transaction, chống ghi nhận lặp, tiền ca và tiền đơn luôn khớp.
- Mở/đóng ca có đối soát, chống hai ca Open và bán trong lúc đóng.
- Dùng lại dựng đơn/định giá hiện có và `IInventoryFulfillmentService`, không nhân đôi logic.

**Non-Goals:**
- VietQR, thẻ, hoàn tiền/đổi trả, voucher/điểm (M4); công thức thuế/giảm cấp đơn (Vy); logic kho (Q. Vinh); tìm kiếm Catalog (T. Vinh); thu/chi tiền mặt giữa ca (schema ghi rõ MVP không có).

## Decisions

**D1. Tách bước dựng đơn khỏi bước lưu.** Tách phần validate + định giá trong `CreateAsync` thành `IOrderService.PrepareAsync(request, cashierUserId)` trả `PreparedOrder` (đơn chưa lưu với các dòng đã chụp giá, hoặc lỗi 400) và để `CreateAsync` gọi lại nó. `PosCheckoutService` gọi `PrepareAsync` ngoài transaction (chỉ đọc, như hiện nay), rồi mở transaction và tự lưu. Thay thế cân nhắc: gọi `CreateAsync` rồi `UpdateStatusAsync` (hai transaction, có khoảng đơn `Pending` mồ côi và không nguyên tử) hoặc nhân đôi mã định giá (vi phạm "không viết lại công thức").

**D2. Thứ tự trong transaction checkout** (mỗi bước thất bại thì rollback toàn bộ):
1. `BeginTransaction`.
2. Khóa ca `SELECT … FOR UPDATE` theo `posSessionId`; sai chủ → 404, không `Open` → 409.
3. Tra `(cashier_user_id, checkout_key)`; có rồi → rollback nhẹ và trả đơn cũ (200).
4. Sinh mã đơn (cùng cơ chế thử lại khi trùng `idx_orders_code`), `Add(order)` với `Status=Pending`.
5. `ReserveAsync` rồi `ConfirmSaleAsync` (khóa thiết bị theo id tăng dần rồi tồn kho theo product/variant, như hiện có); lưu `UnitCost` từng dòng.
6. Kiểm `cashTendered ≥ total` (sớm hơn, trước bước 4, để không khóa vô ích); `changeGiven = cashTendered − total`.
7. Gán `Completed`, `PaidAt = CompletedAt = now`, `PosSessionId`, `CashTendered`, `ChangeGiven`, `CheckoutKey`; `session.CashSalesTotal += total`.
8. `SaveChanges` → `Commit`. Khóa ca được lấy trước khóa kho, và `CloseAsync` chỉ khóa ca, nên không có vòng đợi. Thứ tự khóa này được ghi vào README `Sales` cho Q. Vinh và An.

**D3. Idempotency bằng cột + unique index.** `orders.checkout_key uuid null` với unique index một phần `(cashier_user_id, checkout_key) WHERE checkout_key IS NOT NULL`. Hai request cùng khóa chạy song song: một cái thắng; cái kia gặp `23505` trên index này, rollback, đọc đơn đã lưu rồi trả 200. Không so khớp nội dung request cũ: khóa gắn với một lần bấm Pay, client đổi giỏ phải sinh khóa mới. Thay thế (bảng idempotency riêng, header `Idempotency-Key`) thêm bảng và middleware không cần cho một endpoint.

**D4. Tiền ca là bộ đệm trong `pos_sessions`, đúng schema.** `cash_sales_total` cộng tổng đơn (không cộng tiền khách đưa) một lần trong transaction thanh toán. Đóng ca khóa ca, tính `expected = opening + cash_sales_total` (cộng hoàn tiền mặt từ `order_returns` ở M4), lưu `closing_balance`, `expected_closing_balance`, `closing_note`, `closed_by_user_id`, `closed_at`. `discrepancy` tính ra, không lưu thêm cột.

**D5. Một ca Open mỗi người** bằng partial unique index `uq_pos_sessions_cashier_open` ON `pos_sessions(cashier_user_id) WHERE status = 'Open'`; service kiểm trước để trả 409 sạch, và ánh xạ `23505` của index này sang 409 khi hai request đồng thời.

**D6. Quyền.** Nhận actor từ `AuthenticatedUser.TryRead`, như `OrdersController`. `PosController` ở `backend/Sales/`, `[Authorize(Roles="Admin,Manager,SalesStaff")]` mức lớp, ghi/bán chỉ `Admin,SalesStaff`, đóng ca thêm `Manager`; `SalesStaff` chỉ thấy/đóng ca của mình (404 nếu của người khác). Cập nhật `docs/API_PERMISSIONS.md` và baseline `ApiAuthorizationCoverageTests` cùng lúc.

**D7. Đơn cũ giữ nguyên.** `POST /api/orders` và PATCH status không đổi; thêm kiểm: đơn có `PosSessionId` hoặc đã `Completed` không còn bước chuyển hợp lệ (bảng `OrderStatusTransitions` đã chặn), nên chỉ cần test. Các cột mới ở `OrderDto` nullable để client cũ không gãy.

**D8. UI.** `Repositories/Sales/IPosRepository` (shift + checkout), `ViewModels/Sales/PosViewModel`, `ShiftDialogViewModel`, `Views/Sales/PosPage.cs` + dialog mở/đóng ca bằng C# Markup, route `AppRoute.Pos` hiển thị cho `SalesStaff`/`Admin` qua cơ chế role của shell (An/T. Vinh sở hữu shell; chỉ thêm mục, không sửa cơ chế). Tìm hàng dùng API Catalog hiện có (`ProductsController` tìm SKU/barcode, `SerialImeiService` tìm IMEI); khi T. Vinh hoàn thiện tìm kiếm thì thay chỗ gọi trong `IPosRepository`. `checkoutKey` sinh khi bấm Pay lần đầu của giỏ và giữ đến khi thành công hoặc giỏ đổi. Danh sách theo pattern Skia (hàng ít element, nút "•••" nếu cần).

## Risks / Trade-offs

- [Migration xung đột snapshot với An] → migration riêng, chỉ thêm cột/index, tạo sau khi `dev` có migration Identity; báo An khi merge.
- [`PrepareAsync` đọc ngoài transaction nên giá đổi giữa lúc dựng và lưu] → chấp nhận như `CreateAsync` hiện nay; giá được chụp vào dòng đơn, kho kiểm lại dưới khóa trong bước 5.
- [Khách đưa thiếu tiền sau khi đã khóa] → kiểm `cashTendered` trước khi mở transaction, từ tổng đã tính ở `PrepareAsync`.
- [Vy đổi công thức/làm tròn] → checkout chỉ lưu kết quả của `OrderPricingHelper.Calculate`; test dùng giá trị từ helper, không tự tính.
- [Ca bị đóng lúc bán] → khóa ca ở cả hai phía (D2, D4); test đồng thời có điểm đồng bộ trên PostgreSQL.
- [Reuse khóa sai nội dung] → phía client sinh khóa mới khi giỏ đổi (D8); server không so khớp (D3).
- [Rời trang POS sang trang khác làm mất giỏ và panel đóng ca đang nhập] → Mỗi trang có scope và ViewModel riêng nên trạng thái trang không được giữ (giống `OrderCreatePage`). Ca vẫn nằm ở server, giỏ chưa thanh toán thì không có gì bị ghi. Chấp nhận trong M3; giữ giỏ qua điều hướng cần một store cấp phiên, để sau.
- [Giỏ hàng trắng ở cửa sổ ~1010 px do bảng 4 cột cố định] → Đổi sang hai cột (tên chiếm phần còn lại, nhóm điều khiển Auto) và cột thanh toán 340 px; cần chạy lại ở cửa sổ nhỏ.

## Migration Plan

1. Thêm cột và index bằng một migration `AddPosCashShift`; dữ liệu cũ không đổi (cột nullable, ca cũ không cần `expected_closing_balance`).
2. Cập nhật `schema.dbml` cùng lúc và test snapshot.
3. Rollback: `dotnet ef migrations remove` trước khi áp dụng; sau khi áp dụng, hạ cấp bằng migration xuống, các cột mới chỉ chứa dữ liệu POS.
