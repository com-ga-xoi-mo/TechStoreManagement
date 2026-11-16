# Proposal

## Why

M3 Sprint 2 (T. Vinh): "Hoàn thiện full-text và lọc kết hợp theo giá, thương hiệu, CPU, RAM và specs; giữ phân trang ổn định và tìm chính xác SKU/barcode/IMEI." Đạt cần tìm hàng cho POS. Hiện `q` chỉ khớp nguyên chuỗi con ("macbook m3" không ra "MacBook Air M3"), không tìm theo mô tả và không tìm được bằng IMEI/serial. Lọc giá, thương hiệu, danh mục và specs (`spec=cpu:...`, `spec=ram_gb:16`) đã có và kết hợp được; phân trang đã có khóa phụ `id`.

## What Changes

- `q` tách theo khoảng trắng; mỗi từ phải khớp (không phân biệt hoa thường) ít nhất một trong: tên, SKU, thương hiệu, barcode, mô tả, SKU/barcode/tên biến thể.
- `q` trùng **chính xác** một Serial/IMEI (không phân biệt hoa thường, sau trim) trả về sản phẩm sở hữu serial đó.
- Giữ nguyên các bộ lọc, sort và phân trang ổn định; thêm test kết hợp lọc.
- Client: placeholder ô tìm kiếm ghi rõ "tên, SKU, barcode hoặc IMEI".

## Capabilities

### Modified Capabilities

- `product-catalog`: Keyword search.

## Impact

- Backend: `ProductRepository.SearchAsync` (đọc `SerialImeis` trong cùng `AppDbContext`), `ProductService` chuẩn hóa từ khóa; tests integration PostgreSQL/Service.
- Client: chuỗi placeholder `ProductListPage`.
- Không thêm index/migration: số serial khớp chính xác dùng unique index `serial_number` hiện có.
