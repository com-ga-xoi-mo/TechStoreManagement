# Proposal

## Why

Sprint 1 của `docs/MILESTONE_3_PLAN.md` cần nền tảng tài khoản và quyền trước khi mở vận hành đa vai trò. Hiện Orders cho gọi không token và gán Admin mặc định; chưa có quản lý tài khoản hay refresh endpoint, và JWT đã cấp vẫn dùng được sau logout, khóa tài khoản hoặc đổi quyền.

## What Changes

- **BREAKING**: bảo vệ mọi API nghiệp vụ bằng fallback authentication và bảng quyền theo hành động; Orders chỉ nhận người thao tác từ token hợp lệ, bỏ `DefaultActingUserId`. Catalog và các endpoint gợi ý không còn public.
- Thêm API và màn quản lý tài khoản cho Admin: danh sách/tìm kiếm, tạo, sửa hồ sơ, khóa/mở khóa, gán một role, đặt lại mật khẩu; bảo vệ Admin hoạt động cuối cùng kể cả request đồng thời. Không xóa tài khoản có lịch sử.
- Hoàn thiện login, logout và refresh rotation nguyên tử; kiểm tra trạng thái tài khoản và phiên bản token tại server. **BREAKING**: logout thu hồi mọi phiên của tài khoản; token cũ trước bản nâng cấp phải đăng nhập lại.
- Tích hợp refresh trước khi gửi request sắp hết hạn ở desktop, không tự gửi lại thao tác ghi; giữ bảo vệ phiên mới khỏi response đến muộn và sửa hành vi RememberMe.
- Bổ sung Manager demo theo cơ chế seed hiện có, giữ mật khẩu, trạng thái và quyền người dùng đã có; cập nhật HTTP tests, fixtures và tài liệu bị ảnh hưởng.
- Giữ giới hạn Sprint 1: dashboard vẫn Admin-only và Customers vẫn Admin-only theo specs hiện tại. Mở quyền Manager dashboard/Sales khách hàng, menu toàn bộ theo role, che giá vốn theo role, tìm kiếm nâng cao, nhập hàng và POS thuộc các sprint sau.

## Capabilities

### New Capabilities

- `api-role-authorization`: bảng quyền theo endpoint và hành động, fallback policy, public allowlist, định danh hợp lệ và lỗi 401/403 an toàn.
- `account-management`: quản lý tài khoản qua API và màn con trong Cài đặt, validation, bảo vệ Admin cuối cùng và thu hồi quyền khi thay đổi tài khoản.

### Modified Capabilities

- `admin-authentication`: dùng password hasher đang có, xác thực trạng thái token phía server, refresh rotation và logout thu hồi tức thì từ request kế tiếp.
- `order-management`: thay yêu cầu Acting user tạm thời bằng token thật và quyền đọc/ghi theo role.
- `product-catalog`: yêu cầu đăng nhập cho đọc sản phẩm, gợi ý specs/brands và spec definitions; giữ quyền ghi Admin/Manager.
- `frontend-api-access`: phiên có RememberMe, refresh trước request và xử lý lỗi theo đúng phiên, không replay request ghi.
- `data-seeding`: thêm Manager demo và bảo toàn thay đổi tài khoản qua lần seed lại.

## Impact

- Backend: `Program.cs`; Identity controllers/services/repositories/entities/configurations; Orders và các controllers theo bảng quyền; `Common/Data/DemoSeedRepository` và migrations. Giữ Controller → Service → Repository → AppDbContext, transaction qua IUnitOfWork.
- Database: thêm `users.token_version`, ràng buộc duy nhất username/email không phân biệt hoa thường, đồng bộ migration/model snapshot và `docs/database/schema.dbml`; tái sử dụng indexes refresh token đã có. Không sửa dữ liệu nghiệp vụ hay đổi password hash hiện hữu.
- Client: shared requests/DTOs, Identity repository/models/ViewModels/views, route con Settings và DI; Api/session storage/handler và login/logout. Tái sử dụng C# Markup, PageViewModel và design tokens.
- Verification: backend service/HTTP tests với cấu hình auth production, PostgreSQL tests cho rotation/Admin guard, frontend headless tests, cập nhật live API fixtures và README/bảng quyền. Không thêm thư viện runtime nếu nền tảng hiện có đã đáp ứng.
