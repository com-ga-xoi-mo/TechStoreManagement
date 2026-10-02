# ĐỒ ÁN ỨNG DỤNG QUẢN LÝ VÀ BÁN HÀNG (UDQL2 - 2026)

> **Lưu ý đặc biệt cho Nhóm:**
> Team có quy mô **6 thành viên** nên **được toàn quyền chủ động lựa chọn domain (chủ đề cửa hàng / mô hình kinh doanh thực tế)** để thực hiện đồ án, **không bắt buộc phải dựa vào chữ số cuối của MSSV**.
> Tuy nhiên, mô hình nghiệp vụ được chọn phải đảm bảo đầy đủ độ phức tạp, có bài toán kinh doanh rõ ràng và đủ khối lượng công việc để phân chia hợp lý cho cả 6 thành viên.

---

## 1. Mục tiêu đồ án

Sinh viên xây dựng một **ứng dụng quản lý và bán hàng** cho một cửa hàng thực tế. Ứng dụng cần hỗ trợ các nghiệp vụ cơ bản như quản lý sản phẩm, quản lý danh mục, quản lý khách hàng, lập và quản lý đơn hàng, theo dõi doanh thu và các thông tin liên quan đến hoạt động kinh doanh.

### Quy định về Chủ đề cửa hàng (Domain)
- **Đối với nhóm 6 thành viên:** Nhóm **có quyền tự do chọn domain kinh doanh** phù hợp với sở thích, thế mạnh hoặc ý tưởng khởi nghiệp thực tế của nhóm (ví dụ: chuỗi siêu thị mini, nhà sách & văn phòng phẩm, cửa hàng thời trang/giày dép, cửa hàng mỹ phẩm & làm đẹp, thiết bị công nghệ & phụ kiện, tiệm bánh & đồ uống, cửa hàng thú cưng, nông sản/thực phẩm sạch,...). Không bị giới hạn bởi số cuối MSSV.
- **Bảng đối chiếu chủ đề gốc (tham khảo):**

| Số cuối MSSV | Chủ đề gợi ý |
| :---: | :--- |
| **1, 2** | Cửa hàng cà phê nhỏ hoặc quán ăn nhỏ |
| **3, 4** | Cửa hàng bán quần áo nhỏ hoặc balo, túi xách |
| **5, 6** | Cửa hàng bán điện thoại |
| **7, 8** | Cửa hàng bán laptop hoặc phần cứng máy tính |
| **9, 0** | Cửa hàng bán cây cảnh văn phòng hoặc hoa tươi |

*(Các sinh viên làm cá nhân hoặc nhóm thông thường chọn 1 trong 2 chủ đề tương ứng số cuối MSSV; riêng Team 6 thành viên được chủ động lựa chọn domain mở rộng).*

---

## 2. Giai đoạn báo cáo tiến độ (Giữa kỳ)

Ở giai đoạn báo cáo tiến độ, sinh viên xây dựng phiên bản đầu tiên của ứng dụng với mô hình **một người quản trị (admin)**.

Ứng dụng sử dụng **database server** để lưu trữ và quản lý dữ liệu. Admin có thể thực hiện các chức năng chính như:
- Quản lý sản phẩm (CRUD).
- Quản lý danh mục sản phẩm.
- Thêm, sửa, xóa và tìm kiếm dữ liệu.
- Quản lý thông tin khách hàng.
- Tạo và quản lý đơn hàng.
- Theo dõi trạng thái đơn hàng.
- Xem các thông tin thống kê cơ bản.

> **Mục tiêu của giai đoạn này:** Chứng minh sinh viên có thể xây dựng một ứng dụng quản lý có **giao diện hoàn chỉnh, xử lý đúng nghiệp vụ và kết nối thành công với database server**.

---

## 3. Giai đoạn cuối kỳ

Ở giai đoạn cuối kỳ, sinh viên phát triển ứng dụng thành một hệ thống có kiến trúc hoàn chỉnh hơn, trong đó ứng dụng client giao tiếp với **API server** thay vì truy cập trực tiếp vào database.

Hệ thống phải hỗ trợ **nhiều người dùng với các vai trò và quyền hạn khác nhau (RBAC)**. Nhóm tự thiết kế mô hình phân quyền phù hợp với nghiệp vụ của cửa hàng đã chọn.

### Ví dụ về phân quyền người dùng:
- **Admin:** Quản lý toàn bộ hệ thống, quản lý tài khoản người dùng, sản phẩm và các cấu hình hệ thống.
- **Nhân viên bán hàng (Sales Staff):** Lập đơn hàng, xử lý bán hàng trực tiếp/online, tìm kiếm sản phẩm và xem thông tin cần thiết.
- **Nhân viên kho (Warehouse Staff):** Quản lý nhập/xuất kho, cập nhật số lượng tồn kho và tình trạng sản phẩm.
- **Quản lý (Manager):** Xem báo cáo tài chính, biểu đồ doanh thu, thống kê hiệu suất bán hàng và theo dõi hoạt động kinh doanh.

*(Các vai trò trên chỉ mang tính gợi ý. Nhóm có thể thiết kế các vai trò khác tùy biến theo đặc thù domain đã chọn).*

### Yêu cầu phiên bản cuối kỳ:
- Ứng dụng client giao tiếp với **API server**.
- API server xử lý logic nghiệp vụ và truy cập database.
- Đăng nhập và xác thực người dùng (**Authentication** - JWT / Token / Session).
- Phân quyền theo vai trò (**Authorization - RBAC**).
- Kiểm tra quyền truy cập chặt chẽ đối với từng chức năng ở phía server.
- Quản lý toàn bộ dữ liệu thông qua API.
- Hoàn thiện các nghiệp vụ chính của cửa hàng.
- Có giao diện người dùng trực quan, thẩm mỹ, phù hợp với loại hình kinh doanh đã lựa chọn.

---

## 4. Yêu cầu kiến trúc

Đồ án được phát triển theo hai giai đoạn kiến trúc rõ rệt:

1. **Báo cáo tiến độ (Giữa kỳ):**
   $$\text{Ứng dụng (Client)} \longrightarrow \text{Database Server}$$

2. **Báo cáo nghiệm thu (Cuối kỳ):**
   $$\text{Ứng dụng (Client)} \longrightarrow \text{API Server} \longrightarrow \text{Database Server}$$

> **Quy tắc bắt buộc:** Ở phiên bản cuối kỳ, **client tuyệt đối không được truy cập trực tiếp vào database**. Tất cả thao tác đọc/ghi dữ liệu, xác thực và kiểm tra quyền hạn đều phải đi qua API Server.

---

## 5. Yêu cầu chung

- Nhóm cần phân tích và lựa chọn nghiệp vụ phù hợp với mô hình cửa hàng của mình, tự thiết kế cấu trúc dữ liệu, luồng màn hình (UI/UX) cũng như quy trình xử lý nghiệp vụ.
- Không yêu cầu tất cả các cửa hàng phải có cùng một giao diện hoặc cùng một mô hình dữ liệu.
- Điểm mấu chốt của đồ án là chứng minh được năng lực tiến hóa kiến trúc: từ một ứng dụng quản trị cơ bản kết nối trực tiếp database server lên một **hệ thống đa tầng chuyên nghiệp có API, cơ chế bảo mật (Auth/RBAC) và kiểm thử bài bản**.

---

## 6. Yêu cầu về dữ liệu demo

Nhóm phải chuẩn bị sẵn **dữ liệu demo thực tế, đầy đủ để minh họa toàn bộ các chức năng chính của ứng dụng**. Tuyệt đối không để database trống khi báo cáo.

Dữ liệu demo cần mang tính thực tế, sát với loại hình cửa hàng đã chọn.

### Yêu cầu tối thiểu về số lượng dữ liệu:
- Ít nhất **20 sản phẩm**.
- Ít nhất **5 danh mục sản phẩm** (nếu ngành hàng có phân loại).
- Ít nhất **10 khách hàng**.
- Ít nhất **20 đơn hàng** với nhiều trạng thái khác nhau (Chờ xử lý, Đang giao, Đã hoàn thành, Đã hủy,...).
- **Thông tin sản phẩm đầy đủ:** Tên, mã sản phẩm, giá bán, giá nhập, hình ảnh, số lượng tồn kho, danh mục, trạng thái,...
- **Thông tin đơn hàng chi tiết:** Mã đơn, ngày tạo, khách hàng, danh sách món (chi tiết số lượng, đơn giá), giảm giá, thuế, tổng tiền, phương thức thanh toán, trạng thái.
- **Tính đa dạng:** Dữ liệu phải đủ phong phú để kiểm tra tính năng tìm kiếm, phân trang, lọc nâng cao và vẽ biểu đồ báo cáo thống kê.

*Lưu ý theo từng loại hình kinh doanh:* Cấu trúc dữ liệu có thể tùy biến linh hoạt (Ví dụ: điện thoại có hãng/RAM/bộ nhớ/IMEI; thời trang có size/màu/chất liệu; cà phê có size/topping/độ đường đá; mỹ phẩm có hạn sử dụng/loại da,...).

### Tài khoản demo (Cuối kỳ):
Hệ thống phải có sẵn các tài khoản demo đại diện cho các vai trò để giảng viên kiểm tra nhanh:

| Vai trò | Tài khoản demo gợi ý | Mật khẩu mẫu | Mục đích kiểm thử |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin` | `Admin@123` | Quản trị toàn hệ thống, cấu hình và người dùng |
| **Manager** | `manager` | `Manager@123` | Quản lý doanh thu, xem báo cáo thống kê |
| **Sales Staff** | `staff` | `Staff@123` | Thực hiện bán hàng, lập đơn hàng cho khách |
| **Warehouse** | `warehouse` | `Warehouse@123` | Quản lý kho, nhập/xuất hàng và cập nhật tồn kho |

Thông tin chi tiết các tài khoản demo phải được ghi rõ trong file `README.md`.

---

## 7. Yêu cầu về Git Repository

Toàn bộ mã nguồn đồ án phải được quản lý bằng **Git** và lưu trữ trên một Git repository (GitHub / GitLab / Bitbucket).

Repository phải phản ánh trung thực **toàn bộ quá trình phát triển của dự án từ đầu đến cuối**, không được nộp một repository chỉ có một vài commit dồn vào ngày nộp.

### Yêu cầu triển khai:
- Tạo repository ở chế độ **Private** ngay từ khi khởi động đồ án.
- Tạo **Personal Access Token (PAT)** có quyền đọc (`read`), thời hạn tối thiểu 1 tháng. Chỉ dán PAT này vào file `pat.txt` khi nộp bài trên Moodle, **tuyệt đối không commit PAT vào repo**.
- Các thành viên trong nhóm phải commit và push code thường xuyên theo tiến độ công việc.
- Commit message rõ ràng, ngắn gọn, phản ánh đúng nội dung thay đổi (khuyến khích theo chuẩn *Conventional Commits*).
- **Bảo mật tuyệt đối:** Không commit thông tin nhạy cảm (mật khẩu, API key, JWT secret, chuỗi kết nối Database chứa password,...). Sử dụng file `.env` hoặc file config cục bộ kết hợp `.gitignore`.

### Cấu trúc file `README.md`:
File `README.md` ở thư mục gốc của repository phải có đầy đủ:
1. **Tên đồ án & Chủ đề cửa hàng (Domain)** được nhóm lựa chọn.
2. **Mô tả tổng quan về ứng dụng.**
3. **Danh sách các tính năng chính** của hệ thống.
4. **Kiến trúc hệ thống** (Sơ đồ kiến trúc giữa kỳ & cuối kỳ).
5. **Công nghệ sử dụng** (Framework client, API backend, Database, Thư viện,...).
6. **Hướng dẫn cài đặt & chạy chương trình** từng bước.
7. **Cấu hình kết nối Database và API Server**.
8. **Danh sách tài khoản demo** và phân quyền tương ứng.
9. **Thông tin thành viên nhóm 6 người:**
   - Họ và tên
   - Mã số sinh viên (MSSV)
   - Vai trò đảm nhiệm trong dự án (Frontend, Backend API, Database, Testing, AI,...)
   - Tỷ lệ % đóng góp

### Lịch sử phát triển (Git History gợi ý):
```text
feat: initial project structure
feat(db): design database schema and migrations
feat(products): implement product management UI & CRUD
feat(customers): implement customer management
feat(orders): implement order creation and status tracking
feat(auth): add user authentication mechanism
feat(rbac): implement role-based authorization
feat(api): build RESTful API endpoints for products and orders
fix(orders): handle race condition during inventory deduction
test(api): add API test collection for product endpoints
feat(ai): integrate AI inventory forecast assistant
docs: update README with deployment guide and demo credentials
```

---

## 8. Tích hợp AI (Yêu cầu khuyến khích / Tùy chọn)

> **Mục tiêu:** Sinh viên được khuyến khích ứng dụng các mô hình AI/LLM để giải quyết **bài toán nghiệp vụ thực tế** của cửa hàng.
> **Lưu ý:** Không biến đồ án thành một "chatbot trả lời lan man" vô thưởng vô phạt. AI phải có khả năng tương tác với dữ liệu hoặc quy trình của ứng dụng thông qua API / Function Calling.

### 3 hướng ứng dụng AI thực tế:

#### 1. AI hỗ trợ người quản lý (Business Intelligence)
- Phân tích doanh thu: *"Tuần này sản phẩm nào bán chạy nhất?"*
- Tự động tạo báo cáo kinh doanh định kỳ từ dữ liệu đơn hàng.
- Cảnh báo sản phẩm bán chậm, hàng tồn ứ đọng.
- Gợi ý kế hoạch nhập hàng dựa trên lịch sử bán ra và số lượng tồn kho hiện tại.
- Hỏi đáp dữ liệu nội bộ bằng ngôn ngữ tự nhiên:
  > *"Doanh thu tháng này giảm ở nhóm sản phẩm nào?"* $\longrightarrow$ AI gọi API lấy dữ liệu $\longrightarrow$ Phân tích xu hướng $\longrightarrow$ Đưa ra câu trả lời trực quan.

#### 2. AI hỗ trợ nhân viên bán hàng (Sales Assistant)
- **Tư vấn sản phẩm thông minh dựa trên kho hàng thực:**
  - Cửa hàng laptop: *"Khách có ngân sách 25 triệu, cần máy code và đồ họa nhẹ."* $\longrightarrow$ AI truy vấn DB sản phẩm hiện có và gợi ý 2-3 mẫu máy sát nhất kèm lý do.
  - Cửa hàng thời trang: *"Khách nữ, tìm set đồ công sở thanh lịch dưới 1.5 triệu."*
  - Cửa hàng cà phê: *"Khách thích vị đắng nhẹ, không dùng sữa đặc, có topping thanh mát."*
- **Nguyên tắc:** AI **tuyệt đối không bịa đặt sản phẩm**, mọi gợi ý phải được truy xuất từ cơ sở dữ liệu hệ thống.

#### 3. AI tích hợp sâu với API & Nghiệp vụ (Function Calling / Tool Calling)
Mô hình tương tác:
```text
User (Admin / Staff)
        ↓
    AI Assistant
        ↓
  Tools / API Calls
    ├── get_products(filter)
    ├── search_orders(criteria)
    ├── get_inventory_status()
    ├── get_sales_report(timeframe)
    └── create_draft_order(...)
```
- Quản lý hỏi: *"Doanh thu tháng 8 là bao nhiêu?"* $\longrightarrow$ AI gọi `GET /api/reports/revenue?month=8` $\longrightarrow$ Phân tích và trình bày kết quả.
- Nhân viên hỏi: *"Những mặt hàng nào đang dưới mức tồn an toàn?"* $\longrightarrow$ AI gọi `GET /api/products/low-stock` $\longrightarrow$ Trả về bảng danh sách cảnh báo.

### Ràng buộc bảo mật quan trọng với AI:
> **AI phải tuân thủ nghiêm ngặt phân quyền người dùng (RBAC).**
> - Ví dụ: Nhân viên bán hàng (`SALE_STAFF`) hỏi: *"Hãy cho tôi xem tổng doanh thu công ty và danh sách lương nhân viên"* $\longrightarrow$ AI phải nhận biết người gọi chỉ có quyền bán hàng và từ chối cung cấp dữ liệu nhạy cảm.

### Thang điểm AI (Cộng vào phần AI cuối kỳ):
| Mức độ | Hình thức triển khai | Điểm quy đổi |
| :---: | :--- | :---: |
| **0** | Không tích hợp AI | 0.0 đ |
| **1** | AI chatbot đơn giản, hỏi đáp chung chung ngoài lề | 0.25 đ |
| **2** | AI có sử dụng và hiểu dữ liệu sản phẩm / đơn hàng của hệ thống | 0.5 đ |
| **3** | AI gọi API / Tools nội bộ để thực thi thao tác nghiệp vụ | 0.75 đ |
| **4** | AI Agent toàn diện (đa công cụ, kiểm soát phân quyền RBAC chặt chẽ) | 1.0 đ *(tối đa tiêu chí)* |

---

## 9. Yêu cầu đảm bảo chất lượng và kiểm thử (QA & Testing)

Hệ thống phải được kiểm thử toàn diện để đảm bảo tính ổn định, độ tin cậy của dữ liệu và khả năng xử lý các tình huống bất thường.

### 1. Kiểm thử chức năng (Functional Testing)
Xây dựng danh sách Test Cases cho các tính năng trọng yếu:
- Thêm, sửa, xóa, tìm kiếm sản phẩm & danh mục.
- Quản lý khách hàng.
- Tạo, hủy và cập nhật trạng thái đơn hàng.
- Tính toán tổng tiền, thuế, chiết khấu.
- Trừ kho tự động khi hoàn tất đơn hàng.
- Đăng nhập, đăng xuất, cấp token.
- Phân quyền theo role, kiểm tra chặn API trái quyền.
- Xử lý dữ liệu không hợp lệ.

**Cấu trúc mẫu của một Test Case:**
| Test Case ID | Tên chức năng | Dữ liệu đầu vào (Input) | Kết quả mong đợi (Expected) | Kết quả thực tế (Actual) | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :---: |
| `AUTH-01` | Đăng nhập hợp lệ | Username & password đúng | Cấp Token, vào Dashboard | Đăng nhập thành công | **PASS** |
| `AUTH-02` | Đăng nhập sai pass | Username đúng, mật khẩu sai | Báo lỗi 401, không cấp token | Hiện thông báo lỗi | **PASS** |
| `PROD-01` | Tạo sản phẩm hợp lệ | Đủ trường bắt buộc, giá > 0 | Tạo mới thành công, lưu DB | Sản phẩm xuất hiện | **PASS** |
| `PROD-02` | Giá sản phẩm âm | Giá = -50.000đ | Chặn lại, báo lỗi validate | Báo lỗi giá không hợp lệ | **PASS** |
| `ORD-01` | Tạo đơn hàng chuẩn | Chọn món còn hàng trong kho | Tạo đơn, trừ tồn kho tương ứng | Đơn tạo, kho đã trừ | **PASS** |
| `ORD-02` | Mua vượt số tồn | Đặt mua số lượng > tồn kho | Từ chối tạo đơn, cảnh báo | Báo không đủ hàng | **PASS** |
| `RBAC-01` | Staff gọi API User | Staff token gọi `GET /api/users` | Trả về mã lỗi HTTP 403 Forbidden | Trả về 403 | **PASS** |
| `RBAC-02` | Khách vãng lai gọi API | Request không kèm Token | Trả về mã lỗi HTTP 401 Unauthorized| Trả về 401 | **PASS** |

### 2. Kiểm thử các trường hợp biên (Boundary & Edge Cases)
- Số lượng mua = 0, số lượng âm.
- Giá tiền = 0 hoặc âm.
- Tên sản phẩm để rỗng hoặc chứa chuỗi ký tự cực dài.
- Đơn hàng rỗng (không có sản phẩm nào).
- Xử lý xung đột khi 2 người cùng đặt mua món hàng cuối cùng trong kho.
- Token hết hạn (Expired Token) hoặc token giả mạo.

### 3. Kiểm thử API (API Testing)
- Sử dụng các công cụ như Postman, Insomnia hoặc Swagger/OpenAPI.
- Kiểm tra các HTTP Method (`GET`, `POST`, `PUT`, `DELETE`).
- Kiểm tra HTTP Status Codes chuẩn (`200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `500 Internal Server Error`).
- Định dạng dữ liệu JSON trả về đồng nhất và rõ ràng.

### 4. Kiểm thử phân quyền (RBAC Testing)
- **Quy tắc vàng:** Kiểm tra phân quyền phải diễn ra tại **Server API**, không chỉ dừng lại ở việc ẩn/hiện nút trên giao diện Client.
- Nhân viên không thể xem doanh thu tổng công ty kể cả khi tự gõ URL hoặc gọi API trực tiếp.
- Nhân viên kho không thể can thiệp sửa giá sản phẩm hoặc tạo tài khoản nhân viên khác.

### 5. Kiểm thử hồi quy (Regression Testing)
- Khi thêm tính năng mới hoặc sửa bug, phải chạy lại các test case cũ để đảm bảo không làm gián đoạn các luồng nghiệp vụ đã chạy ổn định.

### 6. Báo cáo chất lượng (Trong báo cáo cuối kỳ)
- Thống kê tổng số test case đã thực hiện, tỷ lệ Pass/Fail.
- Liệt kê các bug phát hiện được trong quá trình kiểm thử và giải pháp đã khắc phục.

### 7. Chất lượng mã nguồn (Code Quality)
- Đặt tên biến, hàm, class chuẩn convention (PascalCase, camelCase rõ nghĩa).
- Tổ chức source code theo mô hình chuẩn (Layered Architecture, MVC, Clean Architecture,...).
- Xử lý ngoại lệ (Try-Catch, Global Exception Filter) có kiểm soát.
- Không hard-code các thông số nhạy cảm.

### 8. Khuyến khích kiểm thử tự động (Automated Testing - Điểm cộng lớn)
- Khuyến khích viết **Unit Test**, **Integration Test** hoặc **Automated API Tests** (sử dụng xUnit, NUnit, Jest, PyTest, Newman,... tùy theo stack công nghệ).
- Kiểm thử tự động luồng nghiệp vụ khép kín:
  $$\text{Đăng nhập} \longrightarrow \text{Tạo đơn hàng} \longrightarrow \text{Kiểm tra tồn kho} \longrightarrow \text{Trừ kho} \longrightarrow \text{Đối chiếu doanh thu}$$

---

## 10. Cơ cấu chấm điểm đồ án

### Tỷ trọng điểm môn học:
- **Môn UDQL2:**
  - **Giữa kỳ (Báo cáo tiến độ):** 3.5 điểm / 10 điểm tổng kết
  - **Cuối kỳ (Báo cáo nghiệm thu):** 4.5 điểm / 10 điểm tổng kết
  - *(Các bài tập hàng tuần chiếm 2.0 điểm còn lại)*
- **Môn Lập trình Desktop:**
  - **Giữa kỳ (Báo cáo tiến độ):** 50% tổng điểm đồ án
  - **Cuối kỳ (Báo cáo nghiệm thu):** 50% tổng điểm đồ án

---

### BẢNG TIÊU CHÍ CHẤM ĐIỂM GIỮA KỲ (3.5 ĐIỂM)

| STT | Tiêu chí đánh giá | Điểm | Mô tả chi tiết |
| :---: | :--- | :---: | :--- |
| **1** | Phân tích nghiệp vụ & Thiết kế | **0.25** | Xác định rõ domain, quy trình nghiệp vụ, đối tượng dữ liệu, sơ đồ hệ thống. |
| **2** | Thiết kế Cơ sở dữ liệu | **0.50** | Bảng, khóa chính, khóa ngoại, ràng buộc toàn vẹn, tránh dư thừa dữ liệu. |
| **3** | Giao diện & Trải nghiệm (UI/UX) | **0.50** | Giao diện rõ ràng, điều hướng logic, form nhập liệu tiện lợi, thông báo kết quả. |
| **4** | Chức năng quản lý dữ liệu (CRUD) | **1.00** | Hoạt động trơn tru: CRUD sản phẩm, danh mục, khách hàng, tạo đơn hàng, tìm kiếm/lọc. |
| **5** | Kết nối Database Server | **0.75** | Kết nối trực tiếp DB server, đọc/ghi/cập nhật dữ liệu ổn định, xử lý lỗi kết nối cơ bản. |
| **6** | Dữ liệu demo | **0.25** | Đủ tối thiểu 20 sản phẩm, 5 danh mục, 10 khách hàng, 20 đơn hàng thực tế. |
| **7** | Git Repository & Quá trình làm | **0.25** | Có repository từ sớm, commit phân bố đều đặn, commit message rõ ràng, README cơ bản. |
| | **TỔNG ĐIỂM GIỮA KỲ** | **3.50** | *(Tương đương 50% điểm đồ án môn Desktop)* |

---

### BẢNG TIÊU CHÍ CHẤM ĐIỂM CUỐI KỲ (4.5 ĐIỂM)

| STT | Tiêu chí đánh giá | Điểm | Mô tả chi tiết |
| :---: | :--- | :---: | :--- |
| **1** | API Server & Kiến trúc hệ thống | **0.75** | Client kết nối qua API; API RESTful chuẩn; HTTP methods/status code chuẩn; Client không chọc DB. |
| **2** | Authentication & Authorization | **0.75** | Đăng nhập/đăng xuất; Token/Session; Phân quyền đa vai trò (RBAC) được kiểm tra tại Server API. |
| **3** | Database & Xử lý nghiệp vụ Server | **0.75** | Logic nghiệp vụ xử lý ở server; quản lý Transaction toàn vẹn (tạo đơn - trừ tồn kho); xử lý đồng thời. |
| **4** | Kiểm thử & Đảm bảo chất lượng | **0.75** | Bảng test case đầy đủ (chức năng, biên, API, RBAC); báo cáo kiểm thử; **điểm cộng nếu có automated test**. |
| **5** | Giao diện & Trải nghiệm hoàn thiện | **0.50** | UI đẹp mắt, đồng bộ, loading state, empty state, validation dữ liệu chặt chẽ. |
| **6** | Git, README & Chất lượng mã nguồn | **0.50** | Lịch sử Git hoàn chỉnh; README chi tiết; tài khoản demo; cấu trúc code sạch, không chứa secrets. |
| **7** | Tích hợp AI (Khuyến khích) | **0.50** | Tích hợp AI giải quyết bài toán nghiệp vụ từ DB; tuân thủ RBAC; AI hỗ trợ quản lý / bán hàng. |
| | **TỔNG ĐIỂM CUỐI KỲ** | **4.50** | *(Tương đương 50% điểm đồ án môn Desktop)* |

---

### Nguyên tắc chấm điểm của Giảng viên:
1. **Không chỉ chấm "chạy được":** Đánh giá xuyên suốt từ Phân tích $\rightarrow$ Thiết kế $\rightarrow$ Mã nguồn $\rightarrow$ API $\rightarrow$ Bảo mật $\rightarrow$ Kiểm thử $\rightarrow$ Tài liệu Git.
2. **Chức năng phải gắn liền với bài toán nghiệp vụ:** Tính năng phải có giá trị thực tế đối với domain đã chọn, không làm chức năng thừa thãi vô nghĩa.
3. **Không tính điểm cho tính năng mang tính hình thức:** Ví dụ: làm login nhưng API không kiểm tra token; phân role nhưng role nào cũng xem được mọi thứ; có chatbot nhưng chỉ trả lời chung chung không liên quan đến cửa hàng.
4. **Ưu tiên tính đúng đắn và hoàn thiện hơn số lượng:** Một hệ thống có 10 chức năng chạy mượt mà, bảo mật chuẩn, có test case đầy đủ sẽ luôn được điểm cao hơn hệ thống ôm đồm 30 chức năng nhưng đầy lỗi và lỏng lẻo.

---

## 11. Hướng dẫn nộp bài

Khi đến hạn nộp bài trên Moodle, nhóm chuẩn bị **5 file văn bản (.txt)** với nội dung như sau:

1. **`repo.txt`**: Chứa duy nhất đường link dẫn tới Git repository của nhóm (Repository phải ở chế độ **Private**, **tuyệt đối không nộp trực tiếp source code**).
2. **`pat.txt`**: Chứa chuỗi **Personal Access Token (PAT)** có quyền đọc (`read`), thời hạn hiệu lực tối thiểu 1 tháng để giảng viên truy cập repository chấm điểm.
3. **`youtube.txt`**: Chứa link video demo toàn bộ các tính năng của đồ án được đăng tải lên YouTube (Cài đặt video ở chế độ **Unlisted - Không công khai** để chỉ những ai có link mới xem được).
4. **`instructions.txt`**: Hướng dẫn chi tiết cách build file thực thi từ mã nguồn, cách cấu hình database server, API server và các biến môi trường cần thiết để chạy dự án.
5. **`info.txt`**:
   - Bảng phân công công việc chi tiết của **6 thành viên trong nhóm** kèm tỷ lệ % hoàn thành.
   - Danh sách các chức năng đã hoàn thành tốt.
   - Danh sách các tính năng chưa hoàn thiện (nếu có).
   - Các chức năng nhóm đã đầu tư nhiều công sức, kỹ thuật nâng cao và đề xuất cộng điểm (ví dụ: Automated Test, AI Agent tích hợp sâu, kiến trúc tối ưu,...).

### Đóng gói tập tin nộp bài:
- Gom cả 5 file trên vào một thư mục và nén lại thành file zip theo định dạng quy định của nhóm:
  $$\text{NhomXX\_}\langle\text{MSSV\_NhomTruong}\rangle\text{.zip}$$
  *(hoặc theo định dạng mã số nhóm được quy định trên lớp)*
- Nộp file `.zip` lên cổng học tập Moodle trước thời hạn đóng bài.
