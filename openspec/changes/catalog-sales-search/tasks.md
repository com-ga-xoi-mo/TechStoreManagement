# Tasks

## 1. Server

- [ ] 1.1 `ProductRepository.SearchAsync`: tách từ (≤ 8), AND từng từ trên các cột cũ + `Description`; OR serial khớp chính xác qua `SerialImeis`.
- [ ] 1.2 Tests PostgreSQL integration (hoặc service với repository thật nếu đã có host): nhiều từ, thiếu một từ, IMEI chính xác/chuỗi con, mô tả, lọc kết hợp + `totalCount`, phân trang ổn định khi trùng tên.

## 2. Client

- [ ] 2.1 Placeholder tìm kiếm Sản phẩm: "Tìm theo tên, SKU, barcode hoặc IMEI".

## 3. Kiểm thử thủ công (AI)

AI tự chạy trên API + database thật, ghi lệnh và kết quả thật.

- [ ] 3.1 Lấy một serial thật từ `GET /api/inventory/serials`; gọi `GET /api/products?q=<serial>` → đúng sản phẩm; `q=<8 ký tự đầu serial>` → không ra qua serial.
- [ ] 3.2 `q` hai từ không liền nhau trong tên → có kết quả; `q` có từ không tồn tại → rỗng.
- [ ] 3.3 Kết hợp `q` + `brand` + `minPrice/maxPrice` + `spec=ram_gb:<n>`; đối chiếu bằng SQL trực tiếp trên PostgreSQL (`psql`) số sản phẩm mong đợi = `totalCount`.
- [ ] 3.4 Duyệt hết các trang `pageSize=2`, xác nhận không trùng/không sót id so với một lần `pageSize=100`.
