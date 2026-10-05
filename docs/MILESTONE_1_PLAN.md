# KẾ HOẠCH TRIỂN KHAI TỔNG QUÁT: MILESTONE 1
## Nền tảng Hệ thống & Cơ sở Dữ liệu (Foundation & Database)
**Dự án:** TechStore Management System  
**Mục tiêu:** Dựng khung Solution .NET 10 (Backend API + Frontend Uno), hoàn thiện Schema PostgreSQL và nạp toàn bộ dữ liệu demo thực tế.

---

## 1. Phân chia Task cho 6 Kỹ sư (Mỗi người 1 Task độc lập)

*Nguyên tắc: Mỗi kỹ sư nhận đúng 1 đầu việc lớn (Single Task Ownership), tự chủ động thiết kế và triển khai trọn vẹn phần việc của mình.*

### [Dev 1 - Lead] Khởi tạo Solution & Hạ tầng Kỹ thuật
* **Nội dung công việc:** Khởi tạo Git repo (Private), cấu hình `.gitignore`, dựng Solution .NET 10 gồm 3 project (`backend`, `frontend`, `shared`), viết `docker-compose.yml` chạy PostgreSQL 16, cấu hình Swagger và dựng khung điều hướng cơ bản (`ShellPage`) cho Client.
* **Kết quả bàn giao:** Chạy được cả `backend` và `frontend` lên giao diện cơ bản; có file Docker bật PostgreSQL chạy ngay.

---

### [Dev 2] Thiết kế CSDL Module Sản phẩm (Catalog)
* **Nội dung công việc:**
  - Thiết kế các thực thể `Product`, `Category`, `ProductVariant` trong `backend/Catalog/` kế thừa `BaseEntity`.
  - Cấu hình lưu thông số kỹ thuật động (CPU, RAM, Pin, Màn hình...) dạng JSON phẳng qua cột `specs JSONB` trong PostgreSQL, thiết lập chỉ mục GIN với `jsonb_path_ops` trên cả `Product` và `ProductVariant`.
  - Cấu hình quan hệ cây danh mục tự tham chiếu (`parent_id`), chặn vòng lặp cha-con.
  - Cấu hình ràng buộc duy nhất tổng hợp `uq_product_variants_id_product` trên `(id, product_id)` của `ProductVariant` để làm đích tham chiếu cho các module kho và bán hàng.
* **Kết quả bàn giao:** Schema 3 bảng Catalog, cấu hình Fluent API, chỉ mục GIN specs và composite key hoàn thiện trong EF Core.

---

### [Dev 3] Thiết kế CSDL Module Kho hàng, Phiếu nhập & Serial/IMEI (Inventory)
* **Nội dung công việc:**
  - Thiết kế 6 thực thể trong `backend/Inventory/`: `InventoryStock`, `InventoryMovement`, `Supplier`, `PurchaseOrder`, `PurchaseOrderItem`, và `SerialImei`.
  - Định nghĩa 3 enum vòng đời: `serial_imei_status` (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`), `purchase_order_status` (`Draft`, `Ordered`, `PartiallyReceived`, `Received`, `Cancelled`), và `inventory_movement_type` (6 giá trị).
  - Cấu hình khóa ngoại tổng hợp `(variant_id, product_id) > product_variants.(id, product_id)` trên `inventory_stocks`, `purchase_order_items`, và `serial_imeis`.
  - Cấu hình tính năng PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` trên `inventory_stocks(product_id, variant_id)` và `purchase_order_items(purchase_order_id, product_id, variant_id)` (`.AreNullsDistinct(false)`).
  - Thiết lập các ràng buộc kiểm tra (Check Constraints): `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`, `ordered_quantity > 0`, `0 <= received_quantity <= ordered_quantity`, regex kiểm tra IMEI 15 số (`^[0-9]{15}$`).
  - Cấu hình partial index `(product_id, status)` WHERE `status = 'InStock'` trên `SerialImei`.
* **Kết quả bàn giao:** Schema 6 bảng Inventory, sổ cái biến động kho bất biến `InventoryMovement`, khóa ngoại kép và ràng buộc chống bán âm hoàn thiện trong EF Core.

---

### [Dev 4] Thiết kế CSDL Module Đơn hàng, POS & Giao dịch VietQR (Orders & Sales)
* **Nội dung công việc:**
  - Định nghĩa các enum dùng chung trong `shared/Enums/`: `OrderStatus` (`Pending`, `Processing`, `Completed`, `Cancelled`, `Refunded`), `PaymentMethod` (`Cash`, `VietQr`, `Card`), `PosSessionStatus` (`Open`, `Closed`), và `VietQrStatus` (`Pending`, `Confirmed`, `Failed`, `Expired`).
  - Thiết kế thực thể `Order`, `OrderItem`, `OrderReturn` trong `backend/Orders/`.
  - Thiết kế thực thể `PosSession`, `VietQrTransaction` trong `backend/Sales/`.
  - Cấu hình khóa ngoại tổng hợp `(variant_id, product_id)` trên `order_items` liên kết tới `product_variants`.
  - Cấu hình liên kết thiết bị đã bán qua `order_items.serial_imei_id` và các ràng buộc kiểm tra giá/tiền không âm (`numeric(18, 2)`).
* **Kết quả bàn giao:** Schema 3 bảng Orders và 2 bảng Sales, cấu hình Fluent API và các enum dùng chung sẵn sàng cho cả Client và Server.

---

### [Dev 5] Thiết kế CSDL Module Khách hàng, Voucher & Chuẩn bị Dữ liệu Mẫu (Customers)
* **Nội dung công việc:**
  - Thiết kế các thực thể `Customer`, `CustomerLoyaltyPoint`, `Voucher` trong `backend/Customers/`.
  - Định nghĩa các enum `CustomerTier` (`Standard`, `Silver`, `Gold`, `Platinum`) và `VoucherType` (`Percentage`, `FixedAmount`).
  - Cấu hình ràng buộc kiểm tra trên `Voucher` (giá trị giảm > 0, % <= 100, `used_count <= usage_limit`, `expires_at > created_at`) và partial index trên `(is_active, expires_at)` WHERE `is_active = true`.
  - Cấu hình ngày sinh `date_of_birth <= CURRENT_DATE` và điểm tích lũy `>= 0`.
  - Chuẩn bị sẵn danh sách dữ liệu thực tế gồm 10 khách hàng mẫu (tên, số điện thoại) và 20 đơn hàng mẫu nhiều trạng thái để phối hợp với Dev 6.
* **Kết quả bàn giao:** Schema 3 bảng Customers, cấu hình voucher và bộ dữ liệu 10 khách, 20 đơn hàng sẵn sàng để nạp CSDL.

---

### [Dev 6] Thiết kế CSDL Module Tài khoản & Viết Data Seeder Tự động (Identity & Seeding)
* **Nội dung công việc:**
  - Thiết kế các thực thể `User`, `Role`, `UserRole`, `RefreshToken` trong `backend/Identity/`.
  - Định nghĩa enum `RoleType` (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) trong `shared/Enums/`.
  - Cấu hình lưu trữ hash SHA-256 của refresh token (`token_hash`) kèm partial index `(user_id, token_hash)` WHERE `is_revoked = false`.
  - Chuẩn bị dữ liệu $\ge 20$ sản phẩm công nghệ thực tế kèm thông số JSONB specs và viết module `DataSeeder.cs` tự động nạp toàn bộ 21 bảng demo vào PostgreSQL khi API khởi động lần đầu.
* **Kết quả bàn giao:** Schema 4 bảng Identity và module `DataSeeder.cs` tự động seed đầy đủ dữ liệu demo tuân thủ toàn bộ ràng buộc và quan hệ của CSDL.
---

## 2. Tiêu chí Nghiệm thu Milestone 1 (Definition of Done)

1. **Database:** Lệnh `docker compose up -d` và `dotnet ef database update` tạo đầy đủ bảng, khóa ngoại, ràng buộc trong PostgreSQL.
2. **Backend:** Chạy `backend` mở được Swagger; CSDL tự động có sẵn $\ge 20$ sản phẩm, $\ge 5$ danh mục, $\ge 10$ khách hàng, $\ge 20$ đơn hàng mẫu.
3. **Frontend:** Chạy `frontend` mở được cửa sổ Desktop Uno Platform trên cả Windows và macOS với khung menu điều hướng cơ bản.
4. **Git:** Code của 6 bạn được merge vào nhánh `develop` thông qua Pull Request sạch sẽ.
