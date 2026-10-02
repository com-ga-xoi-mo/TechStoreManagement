# ROADMAP - TECHSTORE SYSTEM
**Quy mô:** Team 6 Kỹ sư Phần mềm | **Nền tảng:** .NET 10 | **Kiến trúc:** API-First

---

## 1. Phân công Trách nhiệm cho 2 Giai đoạn

### BẢNG 1: GIAI ĐOẠN BÁO CÁO TIẾN ĐỘ (MÔ HÌNH 1 ADMIN)
> **Mục tiêu:** Cả 6 Kỹ sư cùng xây dựng phiên bản đầu tiên của ứng dụng với mô hình **một người quản trị (Admin)**, kết nối CSDL và xử lý đầy đủ các nghiệp vụ quản lý cốt lõi theo đúng yêu cầu đề bài.

| STT | Kỹ sư | Chức năng Admin đảm nhận (Theo đúng đề bài) | Chi tiết công việc thực hiện |
| :---: | :--- | :--- | :--- |
| 1 | **Dev 1 (Lead)** | **Quản trị Hệ thống & Tài khoản** | • Quản lý tài khoản Admin, cấu hình thông tin cửa hàng.<br>• Setup nền tảng: Solution .NET 10, Uno Shell, Base API, kết nối DB. |
| 2 | **Dev 2** | **Quản lý Sản phẩm (CRUD & Tìm kiếm)** | • Thêm, sửa, xóa và tìm kiếm sản phẩm theo tên, giá, thương hiệu.<br>• Lưu trữ thông số cấu hình phần cứng dạng JSON specs; thiết kế Schema PostgreSQL. |
| 3 | **Dev 3** | **Quản lý Danh mục & Tồn kho ban đầu** | • Thêm, sửa, xóa và tìm kiếm danh mục sản phẩm.<br>• Quản lý danh sách Serial / IMEI từng máy và cập nhật số lượng tồn kho ban đầu. |
| 4 | **Dev 4** | **Tạo & Quản lý Đơn hàng** | • Form tạo đơn hàng mới: Chọn khách hàng, chọn sản phẩm, tính tổng tiền, lưu đơn.<br>• Quản lý danh sách và theo dõi trạng thái đơn (Mới tạo, Đang xử lý, Hoàn thành, Hủy). |
| 5 | **Dev 5** | **Quản lý Thông tin Khách hàng** | • Thêm, sửa, xóa, tìm kiếm thông tin khách hàng, lưu số điện thoại và lịch sử mua. |
| 6 | **Dev 6** | **Báo cáo Thống kê cơ bản & Dữ liệu Demo** | • Xem thông tin thống kê cơ bản: Tổng số lượng sản phẩm, doanh thu tổng hợp.<br>• Chuẩn bị & Seeding dữ liệu demo: $\ge 20$ SP, $\ge 5$ danh mục, $\ge 10$ khách, $\ge 20$ đơn. |

---

### BẢNG 2: GIAI ĐOẠN CUỐI KỲ (HỆ THỐNG ĐA VAI TRÒ RBAC & NÂNG CAO)
> **Mục tiêu:** Tách quyền thành hệ thống **Đa vai trò (RBAC)** với giao diện và quyền hạn chuyên biệt cho từng bộ phận, xử lý đồng thời, tích hợp AI Agent và kiểm thử tự động.

| STT | Kỹ sư | Phân hệ & Vai trò đảm nhận (Cuối kỳ) | Chi tiết công việc thực hiện |
| :---: | :--- | :--- | :--- |
| 1 | **Dev 1 (Lead)** | **Quản trị Người dùng & Vòng đời Đơn hàng** | • Quản lý Nhân sự & Phân quyền: Thêm, sửa, khóa tài khoản nhân viên, gán vai trò.<br>• Nghiệp vụ Vòng đời đơn hàng nâng cao: Hủy đơn, hoàn tiền, đổi trả thiết bị. |
| 2 | **Dev 2** | **Tối ưu Catalog & Quản lý Bảo hành** | • Tối ưu hóa Catalog: Tìm kiếm full-text, bộ lọc nâng cao đa thuộc tính (RAM, CPU, Giá).<br>• Quản lý và kích hoạt thời hạn bảo hành theo Serial/IMEI khi đơn hoàn tất. |
| 3 | **Dev 3** | **Phân hệ Thủ kho (Warehouse) & Concurrency** | • Giao diện riêng cho Thủ kho: Quản lý Nhà cung cấp, lập phiếu nhập kho theo lô.<br>• Kỹ thuật: Transaction khóa dòng (`FOR UPDATE`) triệt tiêu Race-Condition khi trừ kho. |
| 4 | **Dev 4** | **Phân hệ Bán hàng (Sales Staff) & POS Quầy** | • Giao diện POS bán hàng chuyên biệt tại quầy cho nhân viên bán hàng (bị khóa xem doanh thu tổng).<br>• Tự động tạo mã VietQR thanh toán động, quản lý ca làm việc (tiền mặt/két tiền). |
| 5 | **Dev 5** | **Phân hệ Quản lý (Manager) & Loyalty** | • Giao diện Dashboard cho Quản lý: Biểu đồ doanh thu nâng cao, phân tích lợi nhuận.<br>• Chương trình khách hàng thân thiết VIP (tích điểm thành viên), áp mã voucher. |
| 6 | **Dev 6** | **Bảo mật RBAC Server & Tích hợp AI Agent** | • Middleware kiểm tra quyền 4 role (`Admin`, `Manager`, `Staff`, `Warehouse`) tại Server API.<br>• Tích hợp AI Gemini Function Calling (Tư vấn bán hàng từ kho thực & Báo cáo BI). |

*Lưu ý: Cả 6 Kỹ sư đều chịu trách nhiệm tự viết Unit Test (`xUnit`) và Integration Test cho module của mình.*

---

## 2. Các Cột mốc Hoàn thành (Feature Milestones)

* **🚩 Milestone 1: Nền tảng & CSDL**
  * Solution .NET 10 (Uno Platform + ASP.NET Core API).
  * Schema PostgreSQL, EF Core Migrations, Seeding dữ liệu thực tế ($\ge 20$ SP, $\ge 5$ danh mục, $\ge 10$ khách, $\ge 20$ đơn).
* **🚩 Milestone 2: Hoàn thiện Phân hệ Quản trị (Admin - Bảng 1)**
  * Cả 6 Kỹ sư hoàn tất 6 chức năng Admin qua API: Sản phẩm, Danh mục, Kho IMEI, Tạo & Quản lý Đơn hàng, Khách hàng, Thống kê cơ bản.
* **🚩 Milestone 3: Tách Phân quyền Đa vai trò (RBAC - Bảng 2)**
  * Tách giao diện riêng cho 4 vai trò (`Admin`, `Manager`, `Sales Staff`, `Warehouse`).
  * Middleware JWT kiểm tra quyền tại Server API (chặn Staff xem doanh thu).
* **🚩 Milestone 4: Xử lý Đồng thời, Kho Nâng cao & Loyalty**
  * Transaction khóa dòng chống bán âm kho; Đổi trả bảo hành theo Serial/IMEI.
  * Tích điểm VIP, tạo mã VietQR thanh toán tự động.
* **🚩 Milestone 5: Tích hợp Trợ lý AI (Gemini Tool Calling) & Dashboard**
  * Dashboard báo cáo doanh thu & lợi nhuận cho Quản lý.
  * AI Gemini Function Calling tư vấn thiết bị theo ngân sách/cấu hình từ kho thực tế, bảo mật theo vai trò RBAC.
* **🚩 Milestone 6: Kiểm thử Tự động & Nghiệm thu Bàn giao**
  * Bộ Test Cases và Automated Tests (`xUnit` + `WebApplicationFactory`).
  * Hoàn thiện `README.md`, video demo unlisted và đóng gói sản phẩm.
