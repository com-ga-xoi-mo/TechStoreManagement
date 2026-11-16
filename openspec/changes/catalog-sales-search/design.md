# Design

## Context

`ProductRepository.SearchAsync` lọc `q` bằng một `ILIKE %q%` trên các cột; specs lọc bằng `@>` JSONB; sort luôn `ThenBy(Id)`. `serial_imeis.serial_number` có unique index.

## Decisions

### D1. AND theo từ thay cho PostgreSQL full-text
Tách `q` thành tối đa 8 từ; mỗi từ thêm một `Where` ILIKE như cũ. Đủ cho tên sản phẩm có model/số ("iphone 15 pro") và không cần `tsvector`/migration. Tiếng Việt có dấu vẫn khớp đúng chữ đã nhập (không bỏ dấu).

### D2. IMEI khớp chính xác, OR với kết quả từ khóa
Điều kiện: `(mọi từ khớp) OR EXISTS serial_imeis s WHERE s.product_id = p.id AND lower(s.serial_number) = lower(q)`. Chỉ khớp chính xác để quét IMEI ra đúng một máy, không lộ serial theo chuỗi con.

## Non-Goals
Không xếp hạng độ liên quan; không bỏ dấu tiếng Việt; không trả trạng thái IMEI trong danh sách (POS của Đạt tra serial qua Inventory).
