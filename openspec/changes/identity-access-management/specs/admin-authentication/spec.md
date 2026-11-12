# Spec Delta

## MODIFIED Requirements

### Requirement: Admin Authentication and Secure Token Issuance
The system SHALL authenticate all four roles using the existing salted password-hash format without rehashing or resetting stored passwords during upgrade. Active users with exactly one assigned system role SHALL receive an HMAC-SHA256 signed JWT with a signing key of at least 256 bits, subject/user ID, username, role, jti, iat, exp and a token version, plus a cryptographically random refresh token stored only as its SHA-256 hash. Configured access and refresh lifetimes SHALL be honored. Invalid credentials, inactive accounts or absent/ambiguous role assignments SHALL return the same safe 401 response without a default SalesStaff role.

#### Scenario: Đăng nhập thành công với thông tin quản trị viên hợp lệ
- **WHEN** any of Admin, Manager, SalesStaff or WarehouseStaff logs in with correct existing credentials
- **THEN** the response is 200 with accessToken, refreshToken, expiresIn, tokenType and that user's current profile and role

#### Scenario: Đăng nhập thất bại do sai mật khẩu hoặc tài khoản không tồn tại
- **WHEN** the username is unknown, password wrong or user inactive
- **THEN** the server returns the same 401 authentication error without revealing which check failed

#### Scenario: Chặn đăng nhập đối với tài khoản đã bị vô hiệu hóa
- **WHEN** an inactive user supplies the correct password
- **THEN** the response is the same safe 401 as invalid credentials, without issuing tokens

#### Scenario: Existing password hashes remain valid
- **WHEN** the upgrade is applied to existing seed or user accounts
- **THEN** their existing passwords still authenticate and their hashes are not replaced by migration or seed

#### Scenario: Missing or multiple roles
- **WHEN** an account has zero or multiple assigned roles
- **THEN** login returns 401 instead of choosing an arbitrary role

### Requirement: Current User Profile Retrieval with JWT Verification
Every protected request SHALL validate signature, issuer, audience and expiry, a nonempty GUID subject, active account, exactly one matching current role and the current token version. Requests authenticated after a revocation transaction commits SHALL reject prior tokens with 401 even before JWT expiry. `GET /api/auth/me` SHALL return the current safe profile on success; nonexistent or inactive users SHALL return 401 rather than 404.

#### Scenario: Lấy profile thành công với Bearer Token hợp lệ
- **WHEN** a current valid token calls GET /api/auth/me
- **THEN** the response is 200 with ID, username, fullName, email, role and the existing profile metadata

#### Scenario: Từ chối truy cập profile khi token không hợp lệ, giả mạo hoặc hết hạn
- **WHEN** a token is missing, expired, forged, revoked, has an invalid subject/version or refers to an inactive or nonexistent account
- **THEN** every protected endpoint including /api/auth/me returns 401

#### Scenario: Old role rejected
- **WHEN** a user's role is changed and the transaction commits
- **THEN** the previous JWT cannot authorize a new request with its former role

### Requirement: Admin Logout, Refresh Token Invalidation and Session Cleansing
`POST /api/auth/logout` SHALL require a current valid user token, revoke all refresh tokens and all outstanding access tokens for that user, and return 200. This account-wide behavior SHALL apply whether an optional refreshToken is supplied or omitted. A supplied token belonging to another user SHALL never revoke that other user's sessions. The desktop SHALL clear its local tokens and persisted session on logout even when the server cannot be reached; offline local logout SHALL NOT be reported as confirmed server revocation.

#### Scenario: Đăng xuất thành công
- **WHEN** an authenticated user logs out successfully
- **THEN** every former access/refresh token for that account is rejected on the next protected or refresh request and the client clears its session

#### Scenario: Chặn tái sử dụng Refresh Token đã bị thu hồi
- **WHEN** a refresh token revoked by logout is sent to the refresh endpoint
- **THEN** the response is 401 and no replacement token is issued

#### Scenario: Foreign refresh token cannot revoke another user
- **WHEN** user A logs out while supplying user B's refresh token
- **THEN** only A's sessions are invalidated

#### Scenario: Network failure during logout
- **WHEN** the logout request fails due to network loss
- **THEN** the client clears local credentials, returns to login and does not claim server-side revocation succeeded

## ADDED Requirements

### Requirement: Single-use refresh token rotation
`POST /api/auth/refresh` SHALL be callable without an access token and accept `{ "refreshToken": "..." }`. It SHALL validate the token hash, expiry, non-revoked status and active owner with exactly one role, consume that token and issue a new access/refresh pair atomically, returning the login response shape. Blank/missing input SHALL return 400; unknown, expired, consumed or revoked tokens SHALL return a safe 401. A failed transaction SHALL consume no token and create no partial replacement. Rotation SHALL use current role and token version. Consumed-token reuse SHALL NOT revoke the successfully issued replacement.

#### Scenario: Refresh after access-token expiry
- **WHEN** the access token has expired but the refresh token is valid
- **THEN** refresh returns 200 with a new pair and the old refresh token can no longer be used

#### Scenario: Concurrent refresh
- **WHEN** two independent requests use the same refresh token concurrently
- **THEN** exactly one returns 200, the other returns 401, and only one replacement refresh token is usable

#### Scenario: Failure during rotation
- **WHEN** persistence fails after preparing rotation
- **THEN** the transaction rolls back both consumption and replacement

#### Scenario: Revocation wins before refresh
- **WHEN** lock, role change, password reset or logout commits before a pending refresh is validated
- **THEN** refresh fails with 401 and creates no new usable session
