# Shared Data Transfer Objects (DTOs)

Thư mục chứa các đối tượng truyền dữ liệu (DTO) dùng chung giữa Backend Web API, Frontend Client và Test Suites, tương ứng với 6 phân hệ của CSDL:

## 1. Catalog Module
- `CategoryDto.cs`: Thông tin danh mục, danh mục cha (`parent_id`) và slug.
- `ProductDto.cs`: Thông tin sản phẩm, giá bán, giá nhập tham chiếu, thông số JSONB (`specs`) và cờ `is_serial_tracked`.
- `ProductVariantDto.cs`: Biến thể phần cứng (màu sắc, cấu hình), SKU riêng, barcode riêng, giá bán riêng và specs override.

## 2. Inventory Module
- `InventoryStockDto.cs`: Số dư thực tế (`quantity`), số lượng giữ chỗ (`reserved_quantity`), số lượng khả dụng (`available_quantity = quantity - reserved_quantity`) và ngưỡng cảnh báo.
- `InventoryMovementDto.cs`: Nhật ký lịch sử biến động kho (`movement_type`, `quantity_change`, `quantity_after`, thời gian, người thực hiện).
- `SupplierDto.cs`: Thông tin nhà cung cấp (tên, người đại diện, số điện thoại, địa chỉ).
- `PurchaseOrderDto.cs`: Đơn nhập hàng NCC, mã PO, trạng thái (`purchase_order_status`), tổng tiền, ngày đặt, ngày nhận.
- `PurchaseOrderItemDto.cs`: Từng dòng hàng nhập, số lượng đặt, số lượng thực nhận, đơn giá nhập.
- `SerialImeiDto.cs`: Thông tin chi tiết từng máy, số Serial, số IMEI 15 số, trạng thái vòng đời (`serial_imei_status`), hạn bảo hành.

## 3. Orders Module
- `OrderDto.cs`: Thông tin đơn hàng (`order_code`, khách hàng, thu ngân, ca bán hàng, tổng tiền, thuế, giảm giá, phương thức thanh toán, trạng thái).
- `OrderItemDto.cs`: Chi tiết từng món trong đơn, đơn giá tại thời điểm mua, số lượng, chiết khấu, thành tiền và IMEI đã liên kết.
- `OrderReturnDto.cs`: Phiếu trả hàng / đổi trả bảo hành, lý do, số tiền hoàn, cờ restock lại kho.

## 4. Customers Module
- `CustomerDto.cs`: Hồ sơ khách hàng, số điện thoại, điểm tích lũy, phân hạng VIP (`customer_tier`).
- `CustomerLoyaltyPointDto.cs`: Lịch sử giao dịch điểm tích lũy (số điểm thay đổi, số dư sau giao dịch, lý do).
- `VoucherDto.cs`: Thông tin mã giảm giá (`code`, loại giảm giá, giá trị giảm, hạn dùng, số lượt còn lại).

## 5. Sales Module
- `PosSessionDto.cs`: Ca làm việc thu ngân, số dư đầu ca, cuối ca, tổng tiền mặt, tổng tiền chuyển khoản VietQR, trạng thái ca.
- `VietQrTransactionDto.cs`: Giao dịch chuyển khoản VietQR, mã giao dịch, số tiền, chuỗi mã QR EMVCo, trạng thái đối soát.

## 6. Identity Module
- `UserDto.cs`: Thông tin nhân viên (tên đăng nhập, email, họ tên, danh sách vai trò).
- `RoleDto.cs`: Thông tin vai trò (`role_type`, mô tả quyền hạn).
- `AuthResponseDto.cs`: Kết quả xác thực (Access Token JWT, Refresh Token, hạn dùng, thông tin User).
