# Kịch bản quay video demo tiến độ giữa kỳ - TechStore Management System

- **Đề tài:** Hệ thống quản lý bán lẻ & kho thiết bị công nghệ cao cấp (TechStore)
- **Công nghệ chính:** ASP.NET Core 10 (Backend Modular Monolith, EF Core 10, PostgreSQL 16) + Uno Platform 6.7 / WinUI 3 (Frontend 100% C# Markup, MVVM, Client Repositories)
- **Thời lượng dự kiến:** 12 - 14 phút
- **Mục tiêu:** Tập trung phô diễn các nghiệp vụ chuyên sâu (Business Logic), tính toàn vẹn dữ liệu, kiểm soát đồng thời (Concurrency Control) và giao diện thuần C# Markup.

---

## Bảng phân bổ thời gian tổng quan

| Phần | Nội dung chính | Thời lượng | Trọng tâm nghiệp vụ / kỹ thuật |
| :--- | :--- | :--- | :--- |
| **Phần 1** | Mở đầu & Kiến trúc hệ thống | 00:00 - 01:30 | 3-Tier API-First, Uno Platform C# Markup, PostgreSQL 16 |
| **Phần 2** | RBAC, Thiết lập & Giao diện động | 01:30 - 03:00 | Đăng nhập JWT, Live Theme Dark/Light chuyển tức thì |
| **Phần 3** | Sản phẩm & Thông số Schema-less JSONB | 03:00 - 05:30 | Dynamic JSONB Specs, GIN Indexing, Quản lý ảnh/biến thể |
| **Phần 4** | Nghiệp vụ Kho & Vòng đời Serial/IMEI | 05:30 - 08:30 | Sổ cái bất biến, ràng buộc trùng lặp IMEI, vòng đời thiết bị |
| **Phần 5** | Đơn hàng, State Machine & Khóa dòng ACID | 08:30 - 11:30 | Khóa dòng `SELECT ... FOR UPDATE`, chống Overselling |
| **Phần 6** | Khách hàng & Tích điểm VIP tự động | 11:30 - 12:45 | Tích điểm, thăng hạng thành viên, lịch sử đơn hàng |
| **Phần 7** | Tổng kết & Kế hoạch giai đoạn cuối kỳ | 12:45 - 13:30 | VietQR tự động, Trợ lý Gemini AI, Mobile app Uno |

---

## Chi tiết kịch bản quay & Lời dẫn từng phần

### PHẦN 1: MỞ ĐẦU & KIẾN TRÚC HỆ THỐNG (00:00 - 01:30)

- **Chuẩn bị trước:** Mở sẵn Swagger UI tại `http://localhost:5176/` trên trình duyệt và Visual Studio / Rider mở cấu trúc thư mục dự án.
- **Hành động trên màn hình:**
  1. Chiếu nhanh Swagger UI với danh sách các API Modules: `/api/products`, `/api/inventory`, `/api/orders`, `/api/customers`, `/api/auth`.
  2. Lướt nhanh cấu trúc thư mục: `backend/` (Modular Monolith), `frontend/TechStore.Client/` (Uno Platform C# Markup), `shared/` (DTOs & Enums dùng chung).
  3. Mở ứng dụng Desktop TechStore tại màn hình **Đăng nhập (Login)**.
- **Lời dẫn (Voiceover):**
  > *"Kính chào thầy và các bạn! Hôm nay em xin đại diện nhóm trình bày video demo tiến độ giữa kỳ cho đồ án Hệ thống quản lý bán lẻ thiết bị công nghệ - TechStore Management System.*  
  > *Về mặt kiến trúc, dự án được xây dựng theo chuẩn 3-Tier API-First: Backend sử dụng ASP.NET Core 10 dạng Modular Monolith kết hợp cùng hệ quản trị cơ sở dữ liệu PostgreSQL 16; Frontend là ứng dụng máy tính đa nền tảng viết bằng Uno Platform 6.7 và WinUI 3. Điều đặc biệt là toàn bộ giao diện đã được chuyển đổi hoàn toàn sang C# Markup thay cho XAML truyền thống, giúp đảm bảo tính an toàn kiểu dữ liệu (type-safe) và tối ưu hóa hiệu năng render.*  
  > *Hôm nay, nhóm sẽ tập trung demo sâu vào các bài toán nghiệp vụ phức tạp của ngành bán lẻ công nghệ: quản lý thiết bị định danh Serial/IMEI, lưu trữ thông số kỹ thuật động JSONB, cỗ máy trạng thái đơn hàng, và kỹ thuật khóa dòng database chống overselling."*

---

### PHẦN 2: RBAC, THIẾT LẬP & GIAO DIỆN ĐỘNG (01:30 - 03:00)

- **Hành động trên màn hình:**
  1. Nhập tài khoản quản trị viên: `admin@techstore.local` và mật khẩu. Bấm **Đăng nhập**.
  2. Ứng dụng điều hướng vào màn hình chính với thanh bên (Navigation Pane) gồm: **Tổng quan, Sản phẩm, Danh mục & Kho, Đơn hàng, Khách hàng, Cài đặt**.
  3. Mở trang **Cài đặt (Settings)**:
     - Thao tác chuyển đổi giao diện từ **Chế độ Sáng (Light Mode)** sang **Chế độ Tối (Dark Mode)**.
     - Chỉ ra các thẻ màu, chữ, panel thay đổi trực tiếp ngay trên màn hình.
     - Giới thiệu nhanh các thiết lập hệ thống: Tỉ lệ tích lũy điểm VIP, VAT, tên chi nhánh.
  4. Mở trang **Tổng quan (Dashboard)**:
     - Lướt qua các thẻ thống kê thời gian thực (StatCards): Doanh thu trong ngày, Đơn hàng đang xử lý, Tổng số thiết bị trong kho, và Cảnh báo sắp hết hàng.
- **Lời dẫn (Voiceover):**
  > *"Hệ thống thực thi phân quyền RBAC nghiêm ngặt với 4 vai trò: Admin, Manager, SalesStaff và WarehouseStaff. Sau khi đăng nhập, Client lưu token an toàn và tự động đính kèm Bearer token qua HttpHandler cho mọi request.*  
  > *Tại trang Cài đặt, toàn bộ hệ thống design token màu sắc được xây dựng bằng C# Code hoàn toàn. Khi chúng ta chuyển đổi giữa Light mode và Dark mode, toàn bộ giao diện phản hồi ngay lập tức theo thời gian thực mà không gặp tình trạng chớp nháy.*  
  > *Trang Tổng quan tổng hợp số liệu kinh doanh, lượng thiết bị IMEI đang có trong kho và các cảnh báo thiếu hàng để nhân viên xử lý kịp thời."*

---

### PHẦN 3: PHÂN HỆ SẢN PHẨM & QUẢN LÝ THÔNG SỐ SCHEMA-LESS JSONB (03:00 - 05:30)

- **Hành động trên màn hình:**
  1. Vào menu **Sản phẩm (Catalog / Products)**:
     - Thử tìm kiếm nhanh theo từ khóa tên hoặc mã SKU.
     - Mở bộ lọc danh mục và bộ lọc **Thông số kỹ thuật động (Dynamic Spec Filters)** (ví dụ: RAM 16GB, Chip M3, Dung lượng Pin...).
  2. Bấm vào một sản phẩm cụ thể để vào **Chi tiết sản phẩm (ProductDetailPage)**:
     - Xem danh sách phiên bản (Variants): Ví dụ iPhone 15 có các bản 128GB, 256GB với giá bán và SKU riêng biệt.
     - Cho thấy bảng thông số kỹ thuật được trích xuất và hiển thị tự động từ cột `JSONB`.
     - Bấm xem bộ sưu tập hình ảnh (**ProductImageGalleryView**), phóng to ảnh cover.
  3. Mở trang **Chỉnh sửa / Thêm mới sản phẩm (ProductEditorPage)**:
     - Giới thiệu cách thêm thuộc tính kỹ thuật động: Chọn thuộc tính từ từ điển `spec_definitions` đã chuẩn hóa (như màn hình, CPU, RAM).
     - Thao tác chọn ảnh bìa tải lên từ máy tính (lưu trữ thông qua dịch vụ Object Storage).
- **Lời dẫn (Voiceover):**
  > *"Đối với ngành thiết bị công nghệ, mỗi dòng sản phẩm có đặc tả kỹ thuật hoàn toàn khác nhau: Laptop cần CPU, RAM, GPU; điện thoại cần Dung lượng pin, Độ phân giải màn hình. Thay vì phải migration database liên tục, TechStore sử dụng cột JSONB specs kết hợp chỉ mục GIN jsonb_path_ops trên PostgreSQL 16.*  
  > *Mỗi thông số được chuẩn hóa qua spec_definitions để hiển thị tên và đơn vị đo lường chuẩn xác. Các phiên bản sản phẩm (Variants) kế thừa và ghi đè thông số của sản phẩm cha linh hoạt.*  
  > *Hệ thống quản lý ảnh cho phép upload tối đa 20 ảnh/sản phẩm, tự động kiểm tra định dạng và kích thước an toàn."*

---

### PHẦN 4: NGHIỆP VỤ KHO & VÒNG ĐỜI SERIAL/IMEI ĐỘC NHẤT (05:30 - 08:30)

- **Hành động trên màn hình:**
  1. Vào menu **Danh mục và Kho (Inventory)**:
     - Tab **Tồn kho (Stock Workspace)**: Cho thấy 3 chỉ số then chốt trên từng dòng sản phẩm:
       + Số lượng vật lý thực tế (`Quantity`).
       + Số lượng đã giữ chỗ cho đơn thanh toán (`ReservedQuantity`).
       + Số lượng thực tế còn bán được (`AvailableQuantity = Quantity - ReservedQuantity`).
     - Tab **Cây danh mục (Category Tree)**: Hiển thị phân cấp cha-con nhiều cấp độ.
  2. Bấm nút hành động **"•••"** trên một dòng sản phẩm -> Mở **Lịch sử biến động kho (StockDetailHistoryDialog)**:
     - Cho thấy danh sách lịch sử biến động từ bảng `inventory_movements`: từng lần nhập hàng, bán hàng, điều chỉnh với số lượng trước/sau, mã tham chiếu và lý do cụ thể.
  3. Bấm **"Nhập kho sản phẩm" (ImportStockDialog)**:
     - Chọn sản phẩm có quản lý Serial/IMEI.
     - Nhập số lượng và đơn giá nhập vốn.
     - **Tình huống cố tình nhập lỗi:** Nhập thử 1 mã IMEI đã có sẵn trong cơ sở dữ liệu -> Bấm Lưu -> Hệ thống thông báo lỗi trùng lặp dữ liệu (`DuplicateKeyException` mã 23505 từ PostgreSQL).
     - **Tình huống hợp lệ:** Nhập danh sách các mã IMEI mới hợp lệ -> Bấm Lưu thành công. Tồn kho tăng ngay lập tức.
  4. Chuyển sang trang **Tra cứu Serial/IMEI (SerialImeiPage)**:
     - Tìm kiếm các IMEI vừa nhập, thấy trạng thái hiện tại là `InStock`.
     - Giải thích sơ đồ vòng đời: `InStock` -> `Reserved` -> `Sold` -> `Returned` / `Defective`.
- **Lời dẫn (Voiceover):**
  > *"Phân hệ Kho là trái tim vận hành của TechStore. Hệ thống phân biệt rõ ràng giữa sản phẩm thông thường và thiết bị định danh IMEI. Mọi biến động kho đều được ghi vào sổ cái bất biến inventory_movements nhằm chống gian lận.*  
  > *Khi nhập kho thiết bị công nghệ, hệ thống kích hoạt ràng buộc duy nhất trên toàn hệ thống cho Serial/IMEI. Nếu nhân viên nhập trùng một IMEI đã tồn tại, cơ chế bắt lỗi DuplicateKeyException sẽ từ chối giao dịch ngay lập tức.*  
  > *Mỗi số IMEI đều trải qua vòng đời nghiêm ngặt: Khi nằm trong kho là InStock, khi khách đặt hoặc thanh toán sẽ chuyển sang Reserved, và chỉ khi xuất hàng thành công mới chuyển thành Sold để kích hoạt hạn bảo hành."*

---

### PHẦN 5: ĐƠN HÀNG, STATE MACHINE & KHÓA DÒNG ACID CHỐNG OVERSELLING (08:30 - 11:30)

- **Hành động trên màn hình:**
  1. Vào menu **Đơn hàng (Orders)**:
     - Hiển thị danh sách các đơn hàng hiện có với trạng thái màu sắc tương ứng (`Pending`, `Processing`, `Completed`, `Cancelled`).
  2. Bấm **"Tạo đơn hàng mới" (OrderCreatePage)**:
     - Tìm và chọn một Khách hàng trong hệ thống (thấy hiển thị hạng VIP và số điểm hiện có).
     - Thêm mặt hàng quản lý IMEI: Chọn đích danh số Serial/IMEI đang `InStock`.
     - Thử nhập số lượng vượt quá số tồn khả dụng (`AvailableQuantity`) -> Hệ thống cảnh báo ngay không cho phép tạo.
     - Điều chỉnh lại số lượng hợp lệ, chọn phương thức thanh toán, bấm **"Tạo đơn hàng"**.
  3. Mở **Chi tiết đơn hàng vừa tạo (OrderDetailDialog)**:
     - Trạng thái hiện tại: `Pending`. Mã đơn hàng dạng `ORD-yyyyMMdd-XXXX` sinh tự động duy nhất.
  4. **Thực hiện chuyển đổi trạng thái (State Machine):**
     - Bấm chuyển trạng thái sang **Đang xử lý / Thanh toán (Processing)**:
       + Backend chạy giao dịch ACID với `SELECT ... FOR UPDATE` trên bảng `inventory_stocks`.
       + Serial/IMEI chuyển ngay từ `InStock` sang `Reserved`.
       + Số lượng `ReservedQuantity` của sản phẩm đó trong kho tự động tăng lên 1, giảm khả năng bán của người khác.
     - Bấm chuyển trạng thái tiếp sang **Hoàn tất (Completed)**:
       + Backend gọi `ConfirmSaleAsync`: Tồn kho vật lý `Quantity` giảm, `ReservedQuantity` giải phóng, Serial/IMEI chuyển vĩnh viễn sang `Sold`.
       + Giá vốn đơn vị (`UnitCost`) tại thời điểm bán được ghim cố định vào đơn hàng để tính lãi ròng.
- **Lời dẫn (Voiceover):**
  > *"Tại phân hệ Đơn hàng, bài toán lớn nhất của bán lẻ đa kênh là chống Overselling - bán quá số lượng thực tế khi có nhiều giao dịch đồng thời.*  
  > *Để giải quyết triệt để, tầng dịch vụ OrderService và InventoryService phối hợp thông qua Unit of Work với giao dịch ACID mức độ cao nhất. Khi đơn hàng chuyển từ Pending sang Processing, hệ thống thực thi khóa dòng SELECT ... FOR UPDATE trên bảng inventory_stocks, giữ chỗ số lượng khả dụng và chuyển Serial/IMEI sang Reserved.*  
  > *Khi bấm Hoàn tất (Completed), hàng được xuất khỏi kho, IMEI chính thức thành Sold, giá vốn bình quân gia quyền tại thời điểm xuất được ghim cố định vào OrderItem, đồng thời kích hoạt chính sách bảo hành và cộng điểm VIP cho khách hàng."*

---

### PHẦN 6: KHÁCH HÀNG & TÍCH ĐIỂM VIP TỰ ĐỘNG (11:30 - 12:45)

- **Hành động trên màn hình:**
  1. Vào menu **Khách hàng (Customers)**:
     - Hiển thị danh sách khách hàng và huy hiệu hạng thẻ VIP (`Standard`, `Silver`, `Gold`, `Platinum`).
  2. Bấm vào khách hàng đã thực hiện đơn hàng ở Phần 5:
     - Chỉ ra điểm tích lũy (`LoyaltyPoints`) và tổng tiền đã mua (`TotalSpent`) được tự động cập nhật cộng dồn chính xác theo giá trị đơn hàng vừa hoàn tất.
     - Mở mục lịch sử mua sắm: Thấy đơn hàng vừa tạo đã xuất hiện trong danh sách lịch sử của khách hàng.
- **Lời dẫn (Voiceover):**
  > *"Phân hệ Khách hàng tự động tính toán tổng chi tiêu và điểm thưởng tích lũy theo từng đơn hàng thành công, tự động nâng hạng thẻ thành viên VIP. Thông tin lịch sử mua sắm minh bạch giúp nhân viên chăm sóc khách hàng và áp dụng các chương trình voucher ưu đãi phù hợp."*

---

### PHẦN 7: TỔNG KẾT & KẾ HOẠCH BẢO VỆ CUỐI KỲ (12:45 - 13:30)

- **Hành động trên màn hình:**
  - Quay lại giao diện trang chủ Tổng quan Dashboard hoặc slide tổng kết các mốc đạt được.
- **Lời dẫn (Voiceover):**
  > *"Tổng kết lại, cho đến tiến độ giữa kỳ, nhóm đã hoàn thành xuất sắc toàn bộ kiến trúc nền tảng và các module nghiệp vụ cốt lõi: từ kiến trúc 3-Tier API-First, giao diện thuần C# Markup hiện đại, đến các nghiệp vụ chuyên sâu về Danh mục, Sản phẩm JSONB, Quản lý kho Serial/IMEI, và quy trình xử lý đơn hàng an toàn concurrency.*  
  > *Để chuẩn bị cho đợt báo cáo cuối kỳ, nhóm sẽ tiếp tục phát triển các tính năng mở rộng tiếp theo: tích hợp cổng thanh toán mã động VietQR tự động khớp đơn, phát triển trợ lý ảo Gemini AI hỗ trợ nhân viên tra cứu kho theo quyền hạn, và ứng dụng di động cho nhân viên cửa hàng.*  
  > *Em xin chân thành cảm ơn thầy cô và các bạn đã chú ý lắng nghe và theo dõi phần demo của nhóm!"*

---

## Danh sách việc cần chuẩn bị trước khi bấm máy quay (Checklist)

1. [ ] **Khởi động Database:** Chạy Docker `docker compose up -d` và đảm bảo PostgreSQL 16 đang hoạt động.
2. [ ] **Chạy Backend:** `dotnet run --project backend/TechStore.Api.csproj --launch-profile http` (Port 5176). Dữ liệu mẫu (roles, admin/staff, sản phẩm, kho, khách hàng) sẽ được `DataSeeder` tự động nạp.
3. [ ] **Chạy Desktop Client:** `dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`.
4. [ ] **Độ phân giải màn hình:** Đặt màn hình quay ở độ phân giải 1080p (`1920x1080`) hoặc tỉ lệ 16:9 chuẩn, phóng to kích thước giao diện nếu cần để video hiển thị chữ rõ nét.
5. [ ] **Chuẩn bị dữ liệu demo:** Chuẩn bị sẵn 1 mã IMEI trùng để thực hiện thao tác báo lỗi ở Phần 4 nhằm tạo ấn tượng mạnh về tính chặt chẽ của hệ thống.
