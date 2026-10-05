# Module Identity & AI Agent (Dev 6 phụ trách)

## 1. Danh sách Thực thể (Entities - Kế thừa BaseEntity)
- `User.cs` (`users`): Tài khoản người dùng, nhân viên cửa hàng.
- `Role.cs` (`roles`): Danh mục vai trò quyền hạn trong hệ thống.
- `UserRole.cs` (`user_roles`): Bảng liên kết nhiều - nhiều giữa người dùng và vai trò, khóa chính tổng hợp `(user_id, role_id)`.
- `RefreshToken.cs` (`refresh_tokens`): Mã làm mới phiên JWT, lưu chuỗi băm SHA-256 kèm cơ chế thu hồi.

## 2. Enum Vai trò Người dùng (RBAC Roles)
- **`RoleType`**:
  - `Admin`: Quản trị toàn hệ thống, cấu hình và nhân sự.
  - `Manager`: Cửa hàng trưởng, xem báo cáo doanh thu, lợi nhuận và audit log.
  - `SalesStaff`: Thu ngân / Bán hàng tại quầy (bị chặn quyền xem tổng doanh thu công ty).
  - `WarehouseStaff`: Quản lý kho hàng, nhập kho NCC và quản lý Serial/IMEI.

## 3. Quy ước Thiết kế & Ràng buộc Schema (Theo schema.dbml)
- **Tài khoản người dùng (users):**
  - `username` và `email` đều là duy nhất trên toàn hệ thống (Unique index `idx_users_username`, `idx_users_email`).
  - Mật khẩu lưu dưới dạng băm an toàn `password_hash` (Argon2id hoặc PBKDF2), không lưu bản rõ.
- **Phân quyền vai trò (roles & user_roles):**
  - `roles.name` là duy nhất (`idx_roles_name`).
  - `user_roles` có khóa chính kép `(user_id, role_id)` và đánh chỉ mục B-tree trên `role_id` (`idx_user_roles_role_id`) để truy vấn nhanh danh sách nhân viên theo quyền.
- **Bảo mật Refresh Token (refresh_tokens):**
  - `token_hash` là duy nhất trên toàn hệ thống (`idx_refresh_tokens_token_hash`). Server chỉ lưu giá trị băm SHA-256 của token; chuỗi token gốc chỉ được trả về cho Client đúng 1 lần khi đăng nhập.
  - Chỉ mục một phần: `(user_id, token_hash)` với điều kiện `WHERE is_revoked = false` (`idx_refresh_tokens_active`) giúp kiểm tra tính hợp lệ của phiên đăng nhập cực nhanh.
  - Khi người dùng đăng xuất: đánh dấu `is_revoked = true`.
- **Dữ liệu Demo & Khởi tạo (DataSeeder):**
  - Viết `backend/Common/Data/DataSeeder.cs` tự động nạp đầy đủ dữ liệu demo vào PostgreSQL 16 khi khởi chạy lần đầu:
    - 4 tài khoản người dùng mẫu: `admin`, `manager`, `staff`, `warehouse` (mật khẩu chuẩn).
    - $\ge 20$ sản phẩm công nghệ thực tế kèm thông số kỹ thuật động JSONB specs.
    - $\ge 5$ danh mục phân cấp.
    - $\ge 10$ khách hàng mẫu nhiều hạng VIP.
    - $\ge 20$ đơn hàng mẫu với đầy đủ các trạng thái và liên kết Serial/IMEI.

## 4. Cấu trúc Thành phần Module
- **`Configurations/`**: `UserConfiguration.cs`, `RoleConfiguration.cs`, `UserRoleConfiguration.cs`, `RefreshTokenConfiguration.cs`.
- **`Services/`**:
  - `IAuthService.cs`, `AuthService.cs`: Xử lý đăng nhập, cấp phát và thu hồi JWT / Refresh Token.
  - `JwtService.cs`: Ký và xác thực JWT Bearer Token chứa Claims vai trò.
  - `GeminiAiAgentService.cs`: Trợ lý AI tích hợp Google Gemini API với Tool/Function Calling tra cứu kho hàng thực tế, tuân thủ nghiêm ngặt phân quyền RBAC.
- **`Controllers/`**: `AuthController.cs` (Đăng nhập, làm mới token, đổi mật khẩu), `AiController.cs` (Chatbot AI hỗ trợ bán hàng).
