# Design

## Context

`ProductsController` có `[Authorize]` cho mọi đọc; `ProductDtoMapper.ToDto` luôn gán `CostPrice`. `ProductSummaryDto` (danh sách) không có giá vốn. `AuthenticatedUser.TryRead` đã đọc role từ token cho Orders.

## Decisions

### D1. Che ở service theo cờ, role đọc ở controller
Controller đọc role bằng `AuthenticatedUser.TryRead` (401 nếu token không đọc được), gọi `GetByIdAsync(id, includeCost: FieldAccess.CanViewProductCost(role))`. Service/mapper chỉ nhận cờ bool, không biết HTTP. Lý do: giữ Controller → Service, service test được không cần ClaimsPrincipal.

### D2. `null` thay vì bỏ field
Giữ tên field, trả `null` cho role không được xem. Client phân biệt "không có quyền" với `0`.

### D3. Một nơi định nghĩa quyền giá vốn
`FieldAccess` (Identity/Services) là bảng duy nhất: `Admin`, `Manager` xem giá vốn sản phẩm. Q. Vinh dùng lại cho Inventory.

## Non-Goals
Không đổi quyền endpoint, không che `SerialImeiDto.UnitCost` (Inventory, Q. Vinh).
