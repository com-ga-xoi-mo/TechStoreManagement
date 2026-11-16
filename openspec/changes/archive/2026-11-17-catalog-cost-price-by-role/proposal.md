# Proposal

## Why

M3 Sprint 2 (T. Vinh): "Kiểm tra quyền trả về giá vốn trong DTO sản phẩm. Không chỉ ẩn giá trên giao diện." Hiện `GET /api/products/{id}` trả `costPrice` cho cả bốn role, nên SalesStaff/WarehouseStaff gọi thẳng API là thấy giá vốn. Requirement chấm điểm yêu cầu phân quyền kiểm tra ở server.

## What Changes

- **BREAKING (contract)**: `ProductDto.costPrice` thành nullable. Server chỉ điền cho `Admin`/`Manager`; `SalesStaff`/`WarehouseStaff` nhận `null`.
- Thêm một quy tắc dùng chung `FieldAccess.CanViewProductCost(role)` trong Identity để Q. Vinh áp dụng cùng bảng cho Inventory/serial.
- Client: `ProductDetail.CostPrice` nullable; trang chi tiết chỉ hiện dòng giá vốn khi server trả giá trị.

## Capabilities

### Modified Capabilities

- `product-catalog`: Product detail trả `costPrice` theo role.
- `frontend-catalog-browsing`: hiển thị giá vốn chỉ khi có.

## Impact

- Backend: `ProductsController.GetById`, `IProductService.GetByIdAsync`, `ProductDtoMapper`, `Identity/Services/FieldAccess.cs`; tests ProductService/HTTP.
- Shared: `ProductDto.CostPrice` → `decimal?`.
- Client: `ProductDetail`, `ProductRepository` mapping/validate, `ProductDetailPage`, `ProductEditorViewModel`; fakes trong tests.
- Không đổi: create/update/status (chỉ A/M gọi được) vẫn trả `costPrice`. Inventory `SerialImeiDto.UnitCost` thuộc Q. Vinh, ghi chờ tích hợp.
