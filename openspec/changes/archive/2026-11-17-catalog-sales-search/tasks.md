# Tasks

## 1. Server

- [x] 1.1 `ProductRepository.SearchAsync`: tách từ (≤ 8), AND từng từ trên các cột cũ + `Description`; OR `SerialNumber` hoặc `Imei` khớp chính xác qua `SerialImeis`.
- [x] 1.2 Tests PostgreSQL integration (hoặc service với repository thật nếu đã có host): nhiều từ, thiếu một từ, serial và IMEI chính xác/chuỗi con (kể cả khi cột còn lại null), mô tả, lọc kết hợp + `totalCount`, phân trang ổn định khi trùng tên.

## 2. Client

- [x] 2.1 Placeholder tìm kiếm Sản phẩm: "Tìm theo tên, SKU, barcode hoặc IMEI".

## 3. Kiểm thử thủ công (AI)

AI tự chạy trên API + database thật, ghi lệnh và kết quả thật.

- [x] 3.1 Lấy một SerialNumber hoặc Imei thật từ `GET /api/inventory/serials`; gọi `GET /api/products?q=<số đầy đủ>` → đúng sản phẩm; `q=<8 ký tự đầu số>` → không ra qua Serial/IMEI.
- [x] 3.2 `q` hai từ không liền nhau trong tên → có kết quả; `q` có từ không tồn tại → rỗng.
- [x] 3.3 Kết hợp `q` + `brand` + `minPrice/maxPrice` + `spec=ram_gb:<n>`; đối chiếu bằng SQL trực tiếp trên PostgreSQL (`psql`) số sản phẩm mong đợi = `totalCount`.
- [x] 3.4 Duyệt hết các trang `pageSize=2`, xác nhận không trùng/không sót id so với một lần `pageSize=100`.

## Kết quả kiểm thử (AI, 2026-11-17)

- 3.2 API thật: `q=USB-C Braided` trả 1 sản phẩm `USB-C 100W Braided Cable`; thêm `impossibleword` trả 0. **Pass**, `/tmp/m3-search-api.log`.
- Phát hiện design cũ chỉ tra `SerialNumber` trong khi 50 bản ghi mẫu chỉ có `Imei`. Người dùng đã duyệt cập nhật proposal/design/spec/tasks và code để tra chính xác cả hai cột; không thay schema/quyền API.
- Các lỗi đã gặp và sửa, không tính lần lỗi là đạt: CS0411 do overload assertion; exact serial chứa `%`, `_`, `\` (sửa equality và escape ILIKE); expected sai ở ca 8 từ có `apple` (đổi sang đúng 4 ID Apple); fixture IMEI không đúng 15 chữ số (đổi dữ liệu test và assertion, giữ nguyên constraint).
- Build solution sau mở rộng IMEI: 0 warning/error, `/tmp/m3-search-approved-build.log`. Suite backend trước khi hoàn tất mở rộng đã có 608 Pass / 1 Fail / 0 Skipped; không coi đó là suite đạt. Kết quả suite cuối ghi trong báo cáo change 6.
- Đã chạy Skia với API thật: `/tmp/m3-search-skia.log`; chưa nhìn được placeholder, binding, theme hoặc resize trên Wayland, không tính startup log là đạt UI.
- Regression PostgreSQL cuối: `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter FullyQualifiedName~ProductSearchPostgresTests`, có `TECHSTORE_TEST_CONNECTION`: **1 Pass / 0 Fail / 0 Skipped**, `/tmp/m3-search-approved-regression-final.log`.
- 3.1 Sau restart API code mới, login Admin 200; IMEI thật `490000000000049` từ `GET /api/inventory/serials` thuộc `09efa9e9-8f17-e3fc-bbd7-f096ecec2c40` (Logitech MX Keys S). Tìm số đầy đủ: 200, đúng 1 ID; `q=49000000`: 200, 0 kết quả. **Pass**.
- 3.3 `q=Dell XPS 15&brand=Dell&minPrice=45000000&maxPrice=45000000&spec=ram_gb:32`: API 200 `totalCount=1`; SQL đếm độc lập trên products với brand/giá/spec và từ khóa trả 1. **Pass**.
- 3.4 Mười trang `pageSize=2` thu 20 ID, khớp `pageSize=100`, không trùng/thiếu. **Pass**. Bằng chứng HTTP/SQL `/tmp/m3-search-api.log`; các ca 3.2–3.4 chạy trước bổ sung cột IMEI, regression PostgreSQL cuối kiểm lại kết hợp lọc và phân trang sau bổ sung.
