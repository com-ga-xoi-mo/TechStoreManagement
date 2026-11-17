# Proposal

## Why

M3 Sprint 4 (Vy): "Sửa `OrderPricingHelper.Calculate` để thuế và giảm giá cấp đơn không còn cố định bằng 0; dùng lại giảm giá dòng đã có... Chốt thứ tự giảm dòng → giảm đơn → thuế, làm tròn và phân bổ từng dòng." Hiện đơn luôn có `discountAmount = 0` và `taxAmount = 0`, nhân viên bán hàng giảm giá dòng không giới hạn, và không có số phân bổ từng dòng để M4 hoàn tiền (An) và báo cáo (Bình) dùng lại. Requirement của thầy yêu cầu đơn có "giảm giá, thuế, tổng tiền" và test "Tính toán tổng tiền, thuế, chiết khấu".

## What Changes

- Một công thức tiền duy nhất tại server (`OrderPricingHelper.Calculate`), theo thứ tự:
  1. Dòng: `totalPrice = unitPrice × quantity − discountAmount` (giữ nguyên).
  2. `subtotal = Σ totalPrice`.
  3. Giảm cấp đơn `discountAmount` (0 ≤ giảm ≤ subtotal, số nguyên đồng), phân bổ về dòng theo tỷ lệ `totalPrice`.
  4. Thuế GTGT `taxAmount = round(( subtotal − discountAmount ) × thuế suất)`, phân bổ về dòng theo phần tiền chịu thuế của dòng.
  5. `totalAmount = subtotal − discountAmount + taxAmount`.
  Làm tròn đến đồng (VND không có đơn vị lẻ), nửa đồng làm tròn lên; phần dư khi phân bổ chia theo phần dư lớn nhất nên tổng các dòng luôn bằng số cấp đơn.
- Thuế suất cấu hình `Pricing:VatRatePercent` (mặc định 10%, thuế suất GTGT phổ thông áp dụng cho điện thoại/máy tính). Giá bán trong danh mục là giá chưa thuế, khớp ví dụ đối chiếu trong M4.
- Quyền giảm giá: `SalesStaff` chỉ được giảm tổng (giảm dòng + giảm đơn) tối đa `Pricing:SalesStaffMaxDiscountPercent` (mặc định 10%) tiền hàng trước giảm; `Admin` không giới hạn ngoài các ràng buộc số tiền.
- `POST /api/orders` nhận thêm `orderDiscountAmount` (tùy chọn).
- **BREAKING (schema, additive)**: lưu `orders.tax_rate`, `order_items.order_discount_amount`, `order_items.tax_amount`; migration `AddOrderPricingAllocation`. Đơn cũ có giá trị 0.
- `OrderDto.taxRate`, `OrderItemDto.orderDiscountAmount`, `OrderItemDto.taxAmount`.

## Capabilities

### Modified Capabilities

- `order-management`: công thức tiền, giảm cấp đơn, thuế, phân bổ, quyền giảm giá, chi tiết đơn trả phân bổ.
- `database-schema`: cột phân bổ và thuế suất.

## Impact

- Backend: `Orders/Services/OrderPricingHelper.cs`, `PricingOptions.cs`, `CreateOrderValidator.cs`, `OrderService.cs` (`CreateAsync` nhận `AuthenticatedUser`), `OrdersController.cs`, entities/configurations Orders, migration, `Program.cs` (bind `Pricing`), `appsettings.json`.
- Shared: `CreateOrderRequest.OrderDiscountAmount`, `OrderDto.TaxRate`, `OrderItemDto.OrderDiscountAmount`, `OrderItemDto.TaxAmount`.
- Docs: `docs/database/schema.dbml`, `backend/Orders/README.md`.
- Tests: công thức, phân bổ/làm tròn, quyền giảm giá, không trừ giảm dòng hai lần, migration/HTTP.
- Không làm: UI nhập giảm cấp đơn và hiển thị thuế ở POS/đơn (Đạt nối theo contract này); VIP/voucher/điểm (M4 Vy).
- Đạt: `PrepareAsync`/checkout gọi `OrderPricingHelper.Calculate` và lưu phân bổ như `CreateAsync`; POS hiển thị tổng từ server (thuế 10% làm tổng khác tổng tạm tính ở client).
