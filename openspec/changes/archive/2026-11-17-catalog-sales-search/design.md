# Design

## Context

`ProductRepository.SearchAsync` lọc `q` bằng một `ILIKE %q%` trên các cột; specs lọc bằng `@>` JSONB; sort luôn `ThenBy(Id)`. Serial/IMEI có hai cột riêng `serial_number` và `imei`; dữ liệu mẫu hiện lưu IMEI ở `imei`.

## Decisions

### D1. AND theo từ thay cho PostgreSQL full-text
Tách `q` thành tối đa 8 từ; mỗi từ thêm một `Where` ILIKE như cũ. Đủ cho tên sản phẩm có model/số ("iphone 15 pro") và không cần `tsvector`/migration. Tiếng Việt có dấu vẫn khớp đúng chữ đã nhập (không bỏ dấu).

### D2. IMEI khớp chính xác, OR với kết quả từ khóa
Điều kiện: `(mọi từ khớp) OR EXISTS serial_imeis s WHERE s.product_id = p.id AND (lower(s.serial_number) = lower(q) OR lower(s.imei) = lower(q))`. Chỉ khớp chính xác để quét Serial/IMEI ra đúng sản phẩm, không lộ số theo chuỗi con. So sánh equality giữ nguyên ký tự `%`, `_`, `\`; ILIKE cho từ khóa phải chỉ định escape tương ứng với helper. Người dùng đã duyệt tra cả hai cột khi kiểm chứng dữ liệu thật; không thêm index/migration hay tuyên bố index thường hỗ trợ `lower(...)`.

## Non-Goals
Không xếp hạng độ liên quan; không bỏ dấu tiếng Việt; không trả trạng thái IMEI trong danh sách (POS của Đạt tra serial qua Inventory).
