# TECH REPORT - TECHSTORE SYSTEM
**Đồ án:** Ứng dụng Quản lý & Bán lẻ Thiết bị Công nghệ (UDQL2 / Lập trình Desktop)  
**Quy mô:** Team 6 Kỹ sư Phần mềm | **Nền tảng:** .NET 10  
**Kiến trúc:** 3-Tier API-First ngay từ đầu (Client ──> API Server ──> PostgreSQL)

---

## 1. Domain: Bán lẻ Thiết bị Công nghệ & Phụ kiện
* **Lý do chọn:** Nghiệp vụ thực tế, dữ liệu phong phú, đủ độ phức tạp phân chia cho 6 thành viên.
* **Đặc thù nghiệp vụ:**
  * **Thiết bị giá trị cao (Laptop, Điện thoại):** Quản lý định danh theo **Serial / IMEI duy nhất** (chuẩn IMEI 15 số), 6 trạng thái vòng đời (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`) và thời hạn bảo hành tính động theo ngày kích hoạt.
  * **Phụ kiện:** Quản lý theo số lượng tồn kho tiêu chuẩn.
  * **Thông số kỹ thuật động:** Lưu cấu hình (RAM, CPU, Pin, Màn hình...) dạng JSON linh hoạt.
  * **Kho & POS:** Nhập kho theo lô, POS bán hàng quét mã, trừ kho an toàn, tích điểm, VietQR.

---

## 2. Kiến trúc Hệ thống (API-First 3-Tier)

```text
[ Desktop Client (Uno Platform) ]
             │
             ▼ HTTPS / RESTful API (JWT + JSON)
  [ Backend API (ASP.NET Core) ]
             │
             ▼ Npgsql (Connection Pool)
     [ PostgreSQL Database ]
```

* **Chiến lược API-First:** Client không bao giờ kết nối trực tiếp database; mọi thao tác đều qua API Server ngay từ giai đoạn đầu.
* **Lợi ích:** Tránh hoàn toàn việc viết code tạm rồi đập đi làm lại; Frontend và Backend có thể phát triển song song thông qua API Contracts / Swagger.

---

## 3. Công nghệ Lựa chọn

| Thành phần | Công nghệ (.NET 10) | Điểm cốt lõi |
| :--- | :--- | :--- |
| **Client** | **Uno Platform** (.NET 10) | • Chạy native trên cả Windows (WinUI 3) và macOS (Skia engine).<br>• Dùng chung 100% code XAML và C#.<br>• MVVM chuẩn với `CommunityToolkit.Mvvm`. |
| **Backend** | **ASP.NET Core Web API** (.NET 10) | • RESTful API chuẩn, hiệu năng cao, Swagger/OpenAPI tự động sinh tài liệu. |
| **Database** | **PostgreSQL 16+** | • Chạy native hoặc Docker trên cả Mac và Windows.<br>• Schema chuẩn hóa 21 bảng, 10 Enums, 100% FK indexed, composite FKs `(variant_id, product_id)`.<br>• Cột `specs JSONB` đánh chỉ mục GIN (`jsonb_path_ops`).<br>• PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` và số dư kho khả dụng.<br>• Sổ cái biến động kho bất biến `inventory_movements`, Transaction & Row Locking (`FOR UPDATE`) chống bán âm kho. |
| **ORM** | **EF Core 10** (`Npgsql`) | • Code-First Migrations, LINQ an toàn, cấu hình Fluent API assembly scanning. |
| **Bảo mật** | **JWT Bearer + RBAC** | • 4 vai trò: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`.<br>• Refresh token SHA-256 (`token_hash`) kèm partial index thu hồi.<br>• Kiểm tra phân quyền 100% tại Server API bằng `[Authorize]`. |
| **AI Agent** | **Google Gemini API** | • Tích hợp **Function Calling / Tool Calling** với API nội bộ.<br>• Tư vấn theo kho thực, không bịa đặt; tuân thủ đúng quyền RBAC. |
| **Testing** | **xUnit + WebApplicationFactory** | • Dev tự viết Unit Test (tính tiền, trừ kho) và Integration Test API. |
