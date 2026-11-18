# Tasks

## 1. Công thức (TDD)

- [x] 1.1 Viết test trước cho `OrderPricingHelper.Calculate`: ví dụ M4 (1.980.000), không trừ giảm dòng hai lần, làm tròn thuế, phân bổ phần dư lớn nhất, dòng 0 đồng, tổng phân bổ luôn khớp, thuế 0%.
- [x] 1.2 Cài `Calculate(lines, orderDiscount, taxRatePercent)` trả tổng và phân bổ; bỏ bản cũ luôn trả 0.

## 2. Service, contract và lưu trữ

- [x] 2.1 `PricingOptions` + bind `Pricing` trong `Program.cs`, `appsettings.json`, kiểm khi khởi động.
- [x] 2.2 `CreateOrderRequest.OrderDiscountAmount`; validator: không âm, số nguyên đồng.
- [x] 2.3 `OrderService.CreateAsync(request, actor)`: giảm đơn ≤ subtotal, quyền giảm giá theo role, lưu thuế suất và phân bổ; `OrdersController` truyền actor. Test trước.
- [x] 2.4 Entities, configurations, migration `AddOrderPricingAllocation`, `schema.dbml`.
- [x] 2.5 `OrderDto.TaxRate`, `OrderItemDto.OrderDiscountAmount`, `OrderItemDto.TaxAmount`; map trong chi tiết đơn.
- [x] 2.6 `backend/Orders/README.md`: công thức, làm tròn, phân bổ, quyền giảm giá, cách Đạt gọi.

## 3. Kiểm thử (AI tự chạy, ghi lệnh và kết quả thật)

- [x] 3.1 Build, backend và frontend unit tests; ghi Pass/Fail/Skipped.
- [x] 3.2 Áp migration lên PostgreSQL thật có dữ liệu cũ; kiểm cột/check constraint và đơn cũ giữ nguyên.
- [x] 3.3 HTTP thật: tạo đơn ví dụ M4 bằng Admin, Sales trong/vượt mức giảm, giảm đơn vượt subtotal/lẻ đồng; đọc lại chi tiết đơn so khớp phân bổ.

## Kết quả kiểm thử (AI, 2026-11-17, Windows 11)

- 3.1 `dotnet build` backend, `Frontend.IntegrationTests`, client `net10.0-desktop`: 0 warning, 0 error. Backend unit có PostgreSQL (`TECHSTORE_TEST_CONNECTION` trỏ PostgreSQL 16 Docker, fixture tự tạo/xóa DB): 643 Pass / 0 Fail / 0 Skipped. Frontend unit: 975 Pass / 0 Fail / 0 Skipped. `Frontend.IntegrationTests` (live, opt-in) chỉ build, chưa chạy. **Pass**
- 3.2 DB `techstore_vy_verify` có sẵn 22 đơn/22 dòng từ trước; API áp `20261117135426_AddOrderPricingAllocation` khi khởi động. Trước/sau: 22 đơn, Σ subtotal 216.080.000, Σ discount 0, Σ tax 0, Σ total 216.080.000 không đổi; `tax_rate`, `order_discount_amount`, `tax_amount` của dữ liệu cũ = 0. Có đủ `ck_orders_tax_rate_range`, `ck_order_items_order_discount_ge_zero`, `ck_order_items_tax_amount_ge_zero`; `UPDATE ... tax_amount = -1` và `tax_rate = 100.01` bị PostgreSQL từ chối. **Pass**
- 3.3 HTTP thật `http://localhost:5287`, sản phẩm Dell UltraSharp U2723QE 15.990.000 (số mong đợi tính độc lập bằng Python `Decimal`, không gọi code C#). **Pass**
  - Admin, 2 dòng, giảm đơn 200.000: 201; subtotal 31.980.000, discount 200.000, taxRate 10, tax 3.178.000, total 34.958.000 (khớp); mỗi dòng 100.000 / 1.589.000, tổng dòng khớp đơn; `GET /api/orders/{id}` trả đúng phân bổ đã lưu.
  - Sales giảm đúng 10% (1.599.000): 201. Sales 10% + 1 đồng: 400 `discount`. Sales giảm dòng + giảm đơn vượt 10%: 400 `discount`. Admin cùng mức: 201.
  - `orderDiscountAmount` 0,5 / -1 / lớn hơn subtotal: 400 `orderDiscountAmount`.
  - Gửi `totalAmount`, `taxAmount`, `unitPrice` giả: bị bỏ qua, total 17.589.000 theo danh mục + VAT.
  - `PATCH` sang `Completed`: 200, tiền và phân bổ không đổi.
- Chưa làm trong change này (theo Non-Goals): UI nhập giảm cấp đơn và hiển thị thuế/phân bổ ở trang tạo đơn, chi tiết đơn và POS (Đạt). Trang tạo đơn chỉ đổi nhãn thành "Tạm tính (chưa gồm thuế GTGT)".
