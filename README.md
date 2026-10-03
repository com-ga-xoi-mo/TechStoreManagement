# TechStore Management System

> **Hệ thống Quản lý và Bán hàng Chuỗi Cửa hàng Thiết bị Công nghệ & Phụ kiện**  
> Môn học: Ứng dụng Quản lý 2 / Lập trình Desktop (Khóa 2026)

---

## 1. Tổng quan Dự án (Project Overview)

### Giới thiệu
**TechStore Management System** là hệ thống phần mềm quản lý và bán hàng toàn diện, phục vụ hoạt động vận hành thường nhật của chuỗi cửa hàng bán lẻ thiết bị công nghệ (điện thoại, laptop, phụ kiện). Hệ thống giải quyết các bài toán nghiệp vụ thực tế như:
- **Bán hàng tại quầy (POS):** Lập đơn nhanh chóng, quét mã vạch/Serial, thanh toán tiền mặt & sinh mã VietQR.
- **Quản lý danh mục & sản phẩm:** Quản lý cấu hình thông số kỹ thuật động qua PostgreSQL JSONB.
- **Quản lý kho & Serial/IMEI:** Theo dõi từng thiết bị giá trị cao theo số Serial/IMEI duy nhất, tình trạng bảo hành và khóa chống bán âm kho (concurrency).
- **Khách hàng & Doanh thu:** Quản lý hội viên, tích điểm thưởng VIP và dashboard thống kê tài chính thời gian thực.
- **Phân quyền & Trợ lý AI:** Phân quyền 4 vai trò (Admin, Manager, Sales Staff, Warehouse Staff) và tích hợp AI Agent hỗ trợ tra cứu thông minh.

### Kiến trúc Hệ thống
Dự án được xây dựng theo mô hình **3-Tier API-First Modern Architecture**:

```text
┌────────────────────────────────────────────────────────────────────────┐
│                   FRONTEND: UNO PLATFORM (WINUI 3)                     │
│   • Multi-platform Desktop: Windows 11 Native & macOS (Skia Desktop)   │
│   • Mô hình giao diện: Feature-First MVVM (CommunityToolkit.Mvvm)      │
│   • Kết nối Backend: RESTful API (JSON) + JWT Bearer                   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTPS / RESTful API
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                   BACKEND: ASP.NET CORE 10 WEB API                     │
│   • Cấu trúc Modular: Catalog, Inventory, Orders, Customers, Identity  │
│   • Bảo mật & Phân quyền: Middleware JWT & RBAC 4 Roles                │
│   • Xử lý nghiệp vụ, ACID Transaction & Khóa Concurrency (FOR UPDATE)  │
│   • Tài liệu API: OpenAPI / Swagger UI                                 │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ EF Core 10 (Npgsql)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        DATABASE: POSTGRESQL 16                         │
│   • Chạy qua Docker Container đồng nhất cho toàn bộ đội ngũ            │
│   • Hỗ trợ cột thông số phần cứng linh hoạt JSONB                      │
└────────────────────────────────────────────────────────────────────────┘
```

### Công nghệ sử dụng
- **Backend API:** ASP.NET Core 10 Web API, Entity Framework Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`), Swagger/OpenAPI.
- **Frontend App:** Uno Platform 6.7 (.NET 10, WinUI 3 XAML, Skia Desktop Engine cho macOS/Linux và Windows App SDK cho Windows).
- **Database:** PostgreSQL 16 (triển khai qua Docker Compose).
- **Kiểm thử tự động:** xUnit, FluentAssertions, Coverlet.
- **Quản lý mã nguồn:** Git, GitHub CLI (`gh`).

---

## 2. Hướng dẫn Cài đặt và Chạy Dự án (How to Run)

### Yêu cầu môi trường
- [.NET SDK 10.0](https://dotnet.microsoft.com/download) trở lên
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (dùng để chạy PostgreSQL 16)
- IDE khuyến nghị: Visual Studio 2022 (v17.12+) / JetBrains Rider / Visual Studio Code (kèm C# Dev Kit)

---

### Các bước khởi chạy từng bước

#### Bước 1: Clone mã nguồn
```bash
git clone https://github.com/com-ga-xoi-mo/TechStoreManagement.git
cd TechStoreManagement
git checkout dev
```

#### Bước 2: Khởi động Cơ sở Dữ liệu PostgreSQL
Đảm bảo **Docker Desktop** đang chạy, sau đó mở terminal tại thư mục gốc dự án và thực thi:
```bash
docker compose up -d
```
> Database PostgreSQL 16 sẽ tự động được khởi tạo tại cổng `5432` với cấu hình:
> - **Host:** `localhost:5432`
> - **Database:** `techstore_db`
> - **Username:** `techstore_user`
> - **Password:** `TechStorePassword123!`

#### Bước 3: Khởi chạy Backend API Server
Mở terminal và chạy lệnh:
```bash
dotnet run --project backend
```
- API Server sẽ khởi động tại: `http://localhost:5000` (hoặc cổng hiển thị trên console).
- Mở trình duyệt truy cập: **`http://localhost:5000`** để xem giao diện **Swagger UI** và thử nghiệm các API (như `/api/health`).

#### Bước 4: Khởi chạy Frontend Desktop App
Mở một cửa sổ terminal mới và chạy:

- **Trên macOS / Linux (Skia Desktop):**
  ```bash
  dotnet run --project frontend/TechStore.Client -f net10.0-desktop
  ```

- **Trên Windows (Native WinUI 3):**
  ```bash
  dotnet run --project frontend/TechStore.Client -f net10.0-windows10.0.26100
  ```

Ứng dụng Desktop sẽ hiển thị giao diện Fluent Design kèm thanh điều hướng (NavigationView) kết nối với hệ thống.

---

### Chạy Kiểm thử Tự động (Automated Tests)
Để chạy toàn bộ bộ kiểm thử tự động của dự án:
```bash
dotnet test TechStore.sln
```
