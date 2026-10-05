# Shared API Request Contracts

Thư mục chứa các Request Contract đại diện cho dữ liệu đầu vào mà Client Desktop gửi lên Backend REST API:

## 1. Catalog Module Requests
- `CreateProductRequest.cs`: Tạo sản phẩm mới (tên, danh mục, SKU, giá, specs JSON).
- `UpdateProductRequest.cs`: Cập nhật thông tin và thông số kỹ thuật sản phẩm.
- `CreateCategoryRequest.cs`: Tạo danh mục mới (tên, slug, danh mục cha `parent_id`).
- `CreateProductVariantRequest.cs`: Tạo biến thể phần cứng (tên biến thể, SKU, barcode, giá, specs override).

## 2. Inventory Module Requests
- `CreatePurchaseOrderRequest.cs`: Lập đơn đặt hàng mua từ nhà cung cấp kèm danh sách dòng hàng.
- `ReceivePurchaseOrderRequest.cs`: Xác nhận tiếp nhận hàng về kho (hỗ trợ nhận từng phần), cập nhật số lượng thực nhận và nạp danh sách Serial/IMEI.
- `StockAdjustmentRequest.cs`: Phiếu kiểm kê kho, điều chỉnh số dư và ghi lý do vào sổ cái `inventory_movements`.
- `UpdateSerialStatusRequest.cs`: Cập nhật trạng thái máy (`UnderRepair`, `Defective`, `Returned`).

## 3. Orders Module Requests
- `CreateOrderRequest.cs`: Lập đơn hàng mới tại quầy POS (khách hàng, ca làm việc, danh sách món, Serial/IMEI chọn mua, voucher áp dụng).
- `UpdateOrderStatusRequest.cs`: Chuyển đổi trạng thái đơn hàng (`Pending` -> `Processing` -> `Completed` / `Cancelled`).
- `CreateOrderReturnRequest.cs`: Lập phiếu đổi trả hàng, lý do, số tiền hoàn và lựa chọn có restock lại kho hay không.

## 4. Customers Module Requests
- `CreateCustomerRequest.cs`: Tạo hồ sơ khách hàng mới (họ tên, số điện thoại, ngày sinh, địa chỉ).
- `UpdateCustomerRequest.cs`: Chỉnh sửa thông tin khách hàng.
- `CreateVoucherRequest.cs`: Tạo mã voucher khuyến mãi (mã code, loại giảm giá, giá trị, hạn sử dụng, giới hạn lượt dùng).

## 5. Sales Module Requests
- `OpenPosSessionRequest.cs`: Mở ca làm việc của thu ngân và khai báo số dư tiền mặt đầu ca.
- `ClosePosSessionRequest.cs`: Đóng ca làm việc và khai báo số dư tiền mặt kiểm đếm cuối ca để đối soát.
- `GenerateVietQrRequest.cs`: Yêu cầu sinh mã thanh toán chuyển khoản VietQR động cho đơn hàng cụ thể.

## 6. Identity Module Requests
- `LoginRequest.cs`: Thông tin đăng nhập tài khoản (`username`, `password`).
- `RefreshTokenRequest.cs`: Chuỗi Refresh Token để cấp lại Access Token JWT mới khi token cũ hết hạn.
- `ChangePasswordRequest.cs`: Đổi mật khẩu tài khoản người dùng.
- `CreateUserRequest.cs`: Tạo tài khoản nhân viên mới và gán vai trò (`role_type`).
