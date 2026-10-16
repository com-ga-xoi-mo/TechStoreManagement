# admin-authentication Specification

## Purpose

Cung cấp khả năng xác thực quản trị viên và nhân viên với tiêu chuẩn bảo mật JWT Bearer token và Refresh Token an toàn, mã hóa mật khẩu, kiểm soát phiên làm việc và thu hồi token tức thì trên hệ thống TechStore.

## Requirements

### Requirement: Admin Authentication and Secure Token Issuance
Hệ thống SHALL xác thực thông tin đăng nhập của người dùng (`username`/`password`) sử dụng cơ chế băm mật khẩu bảo mật (BCrypt với salt), kiểm tra tài khoản còn kích hoạt (`is_active = true`), và cấp phát JWT Bearer Token ký bằng thuật toán HMAC-SHA256 (khóa bí mật tối thiểu 256 bits) chứa các claims tiêu chuẩn (`sub`/`userId`, `unique_name`/`username`, `role`, `jti`, `exp`, `iat`) với thời hạn ngắn (ví dụ 15 - 60 phút). Đồng thời hệ thống cấp phát một cryptographically strong Refresh Token ngẫu nhiên (hạn 7 ngày) được lưu trữ dưới dạng SHA-256 hash (`token_hash`) trong cơ sở dữ liệu `refresh_tokens`.

#### Scenario: Đăng nhập thành công với thông tin quản trị viên hợp lệ
- **WHEN** Admin gửi request `POST /api/auth/login` với username "admin" và mật khẩu chính xác
- **THEN** Hệ thống trả về HTTP 200 OK kèm `accessToken` (JWT), `refreshToken`, hạn sử dụng `expiresIn` và thông tin profile người dùng

#### Scenario: Đăng nhập thất bại do sai mật khẩu hoặc tài khoản không tồn tại
- **WHEN** Người dùng gửi request `POST /api/auth/login` với mật khẩu không đúng hoặc username không tồn tại
- **THEN** Hệ thống trả về HTTP 401 Unauthorized kèm ProblemDetails thông báo lỗi xác thực chung (tránh leak thông tin tài khoản tồn tại)

#### Scenario: Chặn đăng nhập đối với tài khoản đã bị vô hiệu hóa
- **WHEN** Người dùng có `is_active = false` thực hiện đăng nhập với đúng mật khẩu
- **THEN** Hệ thống từ chối và trả về HTTP 403 Forbidden hoặc 401 Unauthorized với thông báo tài khoản bị khóa

### Requirement: Current User Profile Retrieval with JWT Verification
Hệ thống SHALL xác minh tính hợp lệ và chữ ký số của JWT Bearer Token trong header `Authorization: Bearer <token>`, trích xuất thông tin người dùng từ ClaimsPrincipal, và cho phép truy xuất thông tin chi tiết của người dùng đang đăng nhập.

#### Scenario: Lấy profile thành công với Bearer Token hợp lệ
- **WHEN** Người dùng gửi request `GET /api/auth/me` với JWT Bearer Token hợp lệ
- **THEN** Hệ thống trả về HTTP 200 OK kèm `id`, `username`, `fullName`, `email`, `role`, và `lastLoginAt`

#### Scenario: Từ chối truy cập profile khi token không hợp lệ, giả mạo hoặc hết hạn
- **WHEN** Request `GET /api/auth/me` không gửi kèm Bearer Token, gửi token đã hết hạn, hoặc token bị chỉnh sửa chữ ký
- **THEN** Hệ thống trả về HTTP 401 Unauthorized

### Requirement: Admin Logout, Refresh Token Invalidation and Session Cleansing
Hệ thống SHALL thu hồi `refreshToken` trong cơ sở dữ liệu (`is_revoked = true`), chấm dứt phiên làm việc trên server và yêu cầu client xóa sạch token/session khỏi bộ nhớ (`ISessionStore`). Nếu có cơ chế token blacklist/JTI revocation cho access token thì hệ thống ghi nhận để chặn tái sử dụng JWT cũ trước hạn.

#### Scenario: Đăng xuất thành công
- **WHEN** Người dùng gửi request `POST /api/auth/logout` kèm Bearer Token và `refreshToken`
- **THEN** Hệ thống băm `refreshToken` bằng SHA-256, đánh dấu `is_revoked = true` và `updated_at = now()` trong bảng `refresh_tokens`, xóa sạch thông tin phiên trên client và trả về HTTP 200 OK

#### Scenario: Chặn tái sử dụng Refresh Token đã bị thu hồi
- **WHEN** Client cố gắng sử dụng một `refreshToken` đã bị thu hồi (`is_revoked = true`) để refresh hoặc thao tác
- **THEN** Hệ thống từ chối yêu cầu và trả về HTTP 401 Unauthorized

### Requirement: Admin Login View Declarative C# Markup
The desktop client SHALL implement the administrator and staff login screen (`LoginPage.cs`) using Uno Platform C# Markup Fluent API declarations (`Uno.Extensions.Markup`). All UI components, including the branding panel, credential input fields (`TextBox`, `PasswordBox`), submit button with progress ring, error notification banners, and password visibility toggle, SHALL declare layout and data bindings fluently without imperative event subscriptions or manual `PropertyChanged` event wiring.

#### Scenario: User enters credentials and initiates login via declarative binding
- **WHEN** the user types username and password and triggers the login action
- **THEN** the inputs are synchronized via declarative two-way bindings to `LoginViewModel.Username` and `Password`, and the submit button triggers `LoginCommand` with the busy indicator bound to `IsLoggingIn`

#### Scenario: Display authentication error banner via declarative binding
- **WHEN** the view model emits an error state (`HasError = true`, `ErrorMessage`)
- **THEN** the error banner automatically becomes visible and displays the localized error message without imperative code-behind event handling

