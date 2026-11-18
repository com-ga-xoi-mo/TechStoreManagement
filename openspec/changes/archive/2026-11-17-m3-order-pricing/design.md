## Context

`OrderPricingHelper.Calculate(lines)` cộng tổng dòng và trả giảm đơn/thuế bằng 0. `OrderService.CreateAsync` dựng dòng, gọi helper rồi lưu. Đạt (nhánh `feat/m3-pos-cash-shift`, chưa merge) tách phần dựng + định giá thành `PrepareAsync` và checkout POS chỉ lưu kết quả helper. M4 cần số phân bổ đã lưu để hoàn tiền (An) và báo cáo doanh thu chưa thuế (Bình). Ví dụ đối chiếu M4: 2 × 1.000.000đ, giảm đơn 200.000đ, thuế 10% → thu 1.980.000đ; trả 1 món hoàn 900.000 + 90.000 thuế.

## Goals / Non-Goals

**Goals:**
- Một công thức tiền duy nhất ở server, có giảm cấp đơn và thuế khác 0, phân bổ từng dòng khớp tuyệt đối với tổng đơn.
- Kiểm quyền giảm giá theo role.
- Lưu thuế suất và phân bổ để M4 dùng lại, không tính lại.

**Non-Goals:**
- UI nhập giảm cấp đơn, hiển thị thuế trên POS/trang tạo đơn (Đạt nối theo contract).
- VIP, voucher, điểm (M4 Vy); chọn một ưu đãi cấp đơn trong ba nguồn là việc M4.
- Thuế suất theo từng sản phẩm.

## Decisions

**D1. Giá danh mục chưa gồm VAT; thuế cộng thêm.** Schema đã chốt `total = subtotal − discount + tax` và ví dụ M4 dùng thuế cộng thêm, nên giữ cách này thay vì giá đã gồm thuế (cách bán lẻ phổ biến, nhưng sẽ phải đổi công thức schema, dashboard và M4). Ghi rõ trên UI là "Thuế GTGT" cộng vào tổng.

**D2. Thuế tính ở cấp đơn rồi phân bổ.** `taxAmount = round((subtotal − discount) × rate)` như cách ghi "Cộng tiền hàng × thuế suất = Tiền thuế GTGT" trên hóa đơn; sau đó chia về dòng. Thay thế: tính thuế từng dòng rồi cộng — tổng có thể lệch vài đồng so với tính trên cả đơn khi nhiều dòng làm tròn cùng chiều.

**D3. Làm tròn đến đồng, nửa đồng làm tròn lên (`MidpointRounding.AwayFromZero`).** VND không có đơn vị lẻ và tiền mặt không trả được hào; cột vẫn `numeric(18,2)` theo quy ước. Giảm cấp đơn phải là số nguyên đồng (400 nếu lẻ). Giảm dòng giữ quy tắc cũ để không đổi contract đang chạy.

**D4. Phân bổ bằng phần dư lớn nhất (largest remainder).** Mỗi dòng nhận phần làm tròn xuống của `tổng × trọng số / tổng trọng số`; phần đồng còn lại chia lần lượt cho dòng có phần lẻ lớn nhất, hòa thì dòng đứng trước (theo thứ tự request). Tổng phân bổ luôn bằng số cấp đơn, không có "dòng cuối nhận phần dư" phụ thuộc thứ tự. Trọng số giảm đơn là `totalPrice`; trọng số thuế là `totalPrice − orderDiscountAmount` của dòng.

**D5. Hàm thuần.** `OrderPricingHelper.Calculate(lines, orderDiscount, taxRatePercent)` trả `OrderPricing(Totals, Lines)`, không đọc cấu hình hay role. `OrderService` kiểm `orderDiscount ≤ subtotal` và quyền giảm giá trước khi gọi. Đạt gọi đúng hàm này trong `PrepareAsync`, nên POS và `POST /api/orders` cho cùng kết quả.

**D6. Cấu hình `Pricing` qua `IOptions<PricingOptions>`.** `VatRatePercent` (mặc định 10, 0–100, tối đa 2 chữ số thập phân), `SalesStaffMaxDiscountPercent` (mặc định 10, 0–100). Kiểm khi khởi động (`ValidateOnStart`). Không lưu vào `store_settings` vì sửa thuế suất cần ra quyết định kế toán, không phải nút trong UI; M4 có thể chuyển nếu cần. Thuế suất được chụp vào `orders.tax_rate` lúc tạo đơn.

**D7. Quyền giảm giá.** Tổng giảm = Σ giảm dòng + giảm đơn, so với `Σ unitPrice × quantity`. SalesStaff ≤ `SalesStaffMaxDiscountPercent`; Admin chỉ bị giới hạn bởi quy tắc số tiền. Manager không tạo đơn (bảng quyền Orders). Vượt quyền trả 400 dưới khóa `discount` (lỗi nghiệp vụ của request, không phải thiếu quyền vào endpoint, nên không dùng 403). `CreateAsync` nhận `AuthenticatedUser` thay vì `Guid`, giống `SearchAsync`/`UpdateStatusAsync`.

**D8. Lưu trữ.** `orders.tax_rate numeric(5,2)`, `order_items.order_discount_amount numeric(18,2)`, `order_items.tax_amount numeric(18,2)`; mặc định 0 và check `>= 0` (`tax_rate` ≤ 100). Đơn cũ có giảm đơn và thuế bằng 0 nên mặc định 0 là đúng dữ liệu, không cần backfill.

## Risks / Trade-offs

- [Thuế 10% làm tổng thật lớn hơn "tạm tính" ở client] → trang tạo đơn/POS phải hiển thị tổng server trả; ghi trong bàn giao cho Đạt.
- [Migration xung đột snapshot với `AddPosCashShift` của Đạt] → migration chỉ thêm cột; người merge sau tạo lại migration trên `dev` mới. Báo An.
- [Test cũ khẳng định thuế = 0] → fixture dùng thuế suất cấu hình như production; test cũ cần tổng không thuế đặt thuế suất 0 rõ ràng trong fixture riêng của nó.
- [Đổi thuế suất giữa chừng] → đơn đã tạo giữ `tax_rate` cũ; `PATCH status` không tính lại tiền.

## Migration Plan

1. `dotnet ef migrations add AddOrderPricingAllocation --project backend/TechStore.Api.csproj --output-dir Common/Data/Migrations`.
2. Cập nhật `schema.dbml` cùng commit.
3. Rollback trước khi áp dụng: `dotnet ef migrations remove`; sau khi áp dụng: migration xuống chỉ xóa ba cột mới.
