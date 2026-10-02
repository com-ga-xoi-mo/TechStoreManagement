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
* **Nội dung công việc:** Thiết kế các thực thể `Product`, `Category`, `ProductVariant` trong `backend/Catalog/`; cấu hình lưu thông số kỹ thuật động (CPU, RAM, Pin...) bằng cột `specs JSONB` trong PostgreSQL và viết cấu hình Fluent API.
* **Kết quả bàn giao:** Schema bảng sản phẩm, danh mục và cấu hình specs hoàn thiện trong EF Core.

---

### [Dev 3] Thiết kế CSDL Module Kho hàng & Thiết bị Serial/IMEI (Inventory)
* **Nội dung công việc:** Thiết kế các thực thể `InventoryStock`, `Supplier`, `PurchaseOrder` và đặc biệt là `SerialImei` (quản lý mã IMEI duy nhất cho từng máy, trạng thái thiết bị trong kho/đã bán/bảo hành) trong `backend/Inventory/`.
* **Kết quả bàn giao:** Schema bảng kho hàng và quản lý danh sách Serial/IMEI thiết bị hoàn thiện trong EF Core.

---

### [Dev 4] Thiết kế CSDL Module Đơn hàng & Enums Dùng chung (Orders)
* **Nội dung công việc:** Định nghĩa các enum dùng chung (`OrderStatus`, `PaymentMethod`) trong `shared/Enums/`; thiết kế thực thể `Order` và `OrderItem` (chi tiết món, số lượng, đơn giá, mã IMEI đã bán) kèm cấu hình Fluent API trong `backend/Orders/`.
* **Kết quả bàn giao:** Schema bảng đơn hàng và các enum dùng chung sẵn sàng cho cả Client và Server.

---

### [Dev 5] Thiết kế CSDL Module Khách hàng & Chuẩn bị Dữ liệu Mẫu (Customers)
* **Nội dung công việc:** Thiết kế thực thể `Customer`, `LoyaltyPoint` trong `backend/Customers/`; đồng thời chuẩn bị sẵn danh sách dữ liệu thực tế gồm 10 khách hàng mẫu (tên, số điện thoại) và 20 đơn hàng mẫu nhiều trạng thái.
* **Kết quả bàn giao:** Schema bảng khách hàng hoàn thiện và bộ dữ liệu 10 khách, 20 đơn hàng sẵn sàng để nạp CSDL.

---

### [Dev 6] Thiết kế CSDL Module Tài khoản & Viết Data Seeder Tự động (Identity & Seeding)
* **Nội dung công việc:** Thiết kế thực thể `User`, `Role` (Admin, Manager, Staff, Warehouse) trong `backend/Identity/`; chuẩn bị dữ liệu $\ge 20$ sản phẩm công nghệ thực tế kèm specs và viết module `DataSeeder.cs` tự động nạp toàn bộ dữ liệu mẫu vào PostgreSQL khi API khởi động lần đầu.
* **Kết quả bàn giao:** Schema tài khoản người dùng và hệ thống tự động seed dữ liệu demo hoàn chỉnh.

---

## 2. Tiêu chí Nghiệm thu Milestone 1 (Definition of Done)

1. **Database:** Lệnh `docker compose up -d` và `dotnet ef database update` tạo đầy đủ bảng, khóa ngoại, ràng buộc trong PostgreSQL.
2. **Backend:** Chạy `backend` mở được Swagger; CSDL tự động có sẵn $\ge 20$ sản phẩm, $\ge 5$ danh mục, $\ge 10$ khách hàng, $\ge 20$ đơn hàng mẫu.
3. **Frontend:** Chạy `frontend` mở được cửa sổ Desktop Uno Platform trên cả Windows và macOS với khung menu điều hướng cơ bản.
4. **Git:** Code của 6 bạn được merge vào nhánh `develop` thông qua Pull Request sạch sẽ.
