# Spec Delta

## MODIFIED Requirements

### Requirement: Client validation mirrors the API limits
Trước khi gửi, client SHALL kiểm tra: tên (bắt buộc, ≤ 200), SKU sản phẩm khi tạo (bắt buộc, ≤ 64), thương hiệu (bắt buộc, ≤ 100), danh mục (bắt buộc), giá bán và giá vốn (bắt buộc, số thập phân ≥ 0), barcode (≤ 64), mô tả (≤ 2000), và tên, SKU, giá của từng biến thể. Lỗi SHALL hiện ngay dưới ô tương ứng và không có request nào được gửi.

#### Scenario: Several errors at once
- **WHEN** người dùng để trống tên, nhập giá vốn `-1` và để trống giá một biến thể mới rồi bấm Lưu
- **THEN** ba ô đó cùng hiện lỗi và không có request nào được gửi

#### Scenario: Vietnamese number format
- **WHEN** người dùng nhập giá `35.990.000`
- **THEN** giá được hiểu là 35990000 và gửi đúng giá trị đó

## REMOVED Requirements

### Requirement: Image URL preview
**Reason**: Người dùng không còn dán link ảnh. Ảnh được chọn từ máy, tải lên nơi lưu của server và ảnh bìa do server tính (capability `frontend-product-images`, `product-images`).
**Migration**: Dùng khu "Ảnh sản phẩm" trong màn soạn. Request tạo và sửa sản phẩm không còn trường `imageUrl`.
