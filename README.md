# ĐỒ ÁN ỨNG DỤNG QUẢN LÝ VÀ BÁN HÀNG (UDQL2 - 2026)

> **Môn học:** Ứng dụng Quản lý 2 / Lập trình Desktop  
> **Lớp / Khóa:** 2026  
> **Hình thức:** Đồ án nhóm (Team 6 thành viên - Được quyền tự chọn Domain kinh doanh)  

---

## 1. Tên đồ án & Loại cửa hàng (Domain)
- **Tên dự án:** *(Cập nhật tên ứng dụng của nhóm, ví dụ: SmartRetail / CoffeeFlow / TechStore Manager)*
- **Loại hình cửa hàng (Domain):** *(Cập nhật mô hình kinh doanh nhóm chọn, ví dụ: Chuỗi Cửa Hàng Cà Phê & Đồ Uống / Siêu Thị Mini / Cửa Hàng Thiết Bị Công Nghệ /...)*
- **Lý do chọn Domain:** Phù hợp với năng lực, có quy trình nghiệp vụ rõ ràng, đủ độ phức tạp để phân bổ đều cho 6 thành viên.

---

## 2. Mô tả tổng quan về ứng dụng
Hệ thống phần mềm quản lý và bán hàng phục vụ hoạt động vận hành thường nhật của cửa hàng. Hệ thống hỗ trợ xử lý nghiệp vụ bán hàng tại quầy (POS), quản lý danh mục sản phẩm, theo dõi khách hàng thân thiết, quản lý xuất nhập tồn kho, tổng hợp báo cáo doanh thu tài chính theo thời gian thực và tích hợp trợ lý AI thông minh hỗ trợ ra quyết định.

---

## 3. Danh sách các chức năng chính

### Giai đoạn 1: Báo cáo tiến độ (Giữa kỳ)
- [ ] **Quản lý sản phẩm (CRUD):** Thêm, sửa, xóa, xem chi tiết, tìm kiếm và lọc sản phẩm.
- [ ] **Quản lý danh mục:** Phân loại sản phẩm theo danh mục phù hợp với domain.
- [ ] **Quản lý khách hàng:** Lưu trữ thông tin khách hàng, số điện thoại, lịch sử mua hàng.
- [ ] **Lập và quản lý đơn hàng:** Tạo đơn bán hàng, chọn sản phẩm, tính tiền, lưu đơn vào cơ sở dữ liệu.
- [ ] **Theo dõi trạng thái đơn hàng:** Quản lý các trạng thái đơn (Mới tạo, Đang xử lý, Hoàn thành, Hủy).
- [ ] **Báo cáo thống kê cơ bản:** Thống kê tổng số lượng sản phẩm, doanh thu tổng hợp.
- [ ] **Kết nối Database Server:** Đọc/ghi dữ liệu trực tiếp lên Database Server ổn định.

### Giai đoạn 2: Báo cáo nghiệm thu (Cuối kỳ)
- [ ] **Kiến trúc đa tầng (API-First):** Client hoàn toàn không kết nối trực tiếp DB, mọi thao tác thông qua RESTful API Server.
- [ ] **Authentication:** Đăng nhập, đăng xuất, cấp phát Token bảo mật (JWT).
- [ ] **Role-Based Access Control (RBAC):** Phân quyền chặt chẽ các vai trò (Admin, Manager, Sales Staff, Warehouse Staff) được kiểm tra tại Server.
- [ ] **Nghiệp vụ nâng cao:** 
  - Quản lý kho, cảnh báo tồn kho tối thiểu.
  - Quản lý khuyến mãi, chiết khấu, tích điểm thành viên.
  - Xử lý đồng thời (concurrency) và Transaction an toàn khi tạo đơn hàng.
- [ ] **Tích hợp Trợ lý AI (AI Integration):**
  - AI phân tích doanh thu & dự báo hàng tồn kho cho Quản lý.
  - AI tư vấn gợi ý sản phẩm phù hợp ngân sách & sở thích cho Nhân viên bán hàng.
  - Tool/Function calling kết nối với API nội bộ, tuân thủ đúng quyền RBAC.
- [ ] **Kiểm thử chất lượng:** Bộ test case chức năng, test case biên, kiểm thử API Postman và Automated Tests.

---

## 4. Kiến trúc hệ thống

```text
GIAI ĐOẠN GIỮA KỲ:
[ Client App (Admin UI) ] ──────────────> [ Database Server ]

GIAI ĐOẠN CUỐI KỲ:
[ Client App (Multi-Role UI) ]
            │
            ▼ (HTTPS / RESTful API + JWT)
   [ Backend API Server ] ──────────────> [ Database Server ]
            ▲
            │ (Tool Calling / Function Calling)
    [ AI Assistant / LLM ]
```

---

## 5. Công nghệ sử dụng
- **Client (Desktop App):** C# / .NET (WPF / WinForms / Avalonia UI)
- **Backend API:** ASP.NET Core Web API (.NET 8 / .NET 9)
- **Database:** Microsoft SQL Server / PostgreSQL / MySQL
- **ORM / Data Access:** Entity Framework Core / Dapper
- **AI Integration:** OpenAI API / Gemini API / Claude API (Function Calling)
- **Testing & Tools:** xUnit / NUnit, Postman, Git

---

## 6. Hướng dẫn cài đặt và chạy chương trình

### Yêu cầu môi trường
- .NET SDK (8.0 trở lên)
- Hệ quản trị CSDL: SQL Server / PostgreSQL
- IDE: Visual Studio 2022 / JetBrains Rider / VS Code

### Các bước khởi chạy
1. **Clone repository:**
   ```bash
   git clone <URL_REPO>
   cd UDQL2-2026
   ```
2. **Cấu hình Database & API:**
   - Xem mục [7. Cấu hình Database & API Server](#7-cấu-hình-database--api-server).
3. **Chạy Database Migration & Seed Data:**
   ```bash
   # (Cập nhật lệnh tương ứng của nhóm)
   ```
4. **Khởi chạy API Server:**
   ```bash
   dotnet run --project src/Server
   ```
5. **Khởi chạy Client App:**
   ```bash
   dotnet run --project src/Client
   ```

---

## 7. Cấu hình Database & API Server
- Tạo file `appsettings.Development.json` hoặc file `.env` (tuyệt đối không commit file chứa mật khẩu lên git).
- Cung cấp file mẫu `appsettings.example.json`:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost;Database=UDQL2_DB;User Id=sa;Password=YourSecurePassword;TrustServerCertificate=True;"
    },
    "Jwt": {
      "Secret": "YourSuperSecretKeyWithAtLeast32CharactersLong",
      "Issuer": "UDQL2Server",
      "Audience": "UDQL2Client"
    }
  }
  ```

---

## 8. Danh sách tài khoản demo (Kiểm thử phân quyền)

| Vai trò | Tên đăng nhập | Mật khẩu mẫu | Quyền hạn chính |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | `Admin@123` | Quản trị toàn hệ thống, cấu hình người dùng |
| **Manager** | `manager` | `Manager@123` | Xem báo cáo doanh thu, duyệt kế hoạch |
| **Sales Staff**| `staff` | `Staff@123` | Bán hàng, tạo đơn hàng, tra cứu sản phẩm |
| **Warehouse** | `warehouse` | `Warehouse@123` | Quản lý kho, nhập/xuất tồn kho |

---

## 9. Danh sách thành viên nhóm (Team 6 thành viên)

| STT | Họ và tên | MSSV | Vai trò chính trong dự án | % Đóng góp |
| :---: | :--- | :---: | :--- | :---: |
| 1 | **Nguyễn Bảo An** *(Nhóm trưởng)* | **23120207** | Kiến trúc hệ thống, Quản lý dự án, Backend API | 100% |
| 2 | *(Thành viên 2)* | `MSSV_02` | Thiết kế CSDL, Data Seeding, Backend Services | 100% |
| 3 | *(Thành viên 3)* | `MSSV_03` | Phát triển Client UI (Giao diện bán hàng & Dashboard) | 100% |
| 4 | *(Thành viên 4)* | `MSSV_04` | Phát triển Client UI (Quản lý sản phẩm, kho, đơn hàng) | 100% |
| 5 | *(Thành viên 5)* | `MSSV_05` | Xác thực & Phân quyền (Auth/RBAC), Tích hợp AI Assistant | 100% |
| 6 | *(Thành viên 6)* | `MSSV_06` | Đảm bảo chất lượng, Viết Test Cases & Automated Testing | 100% |
