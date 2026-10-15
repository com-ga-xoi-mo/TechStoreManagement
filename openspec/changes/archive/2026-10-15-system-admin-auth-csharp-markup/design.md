# Design

## Context
See proposal.md and MILESTONE_2_PLAN.md Sprint 2 Dev 1 Lead assignment.
The application currently has entities `User`, `Role`, `UserRole`, `RefreshToken` in `backend/Identity/` with seed data for the default admin (`admin` / `Admin@123`). The frontend desktop client currently uses legacy XAML for `ShellPage` and `SettingsPage`. Sprint 2 mandates migrating to 100% C# Markup using `Uno.Extensions.Markup` and modern UI libraries (WinUI CommunityToolkit `SettingsControls`), integrating Admin Login, Shell active user profile reflection, and Store Settings management.

## Goals / Non-Goals

**Goals:**
- **Backend Auth API & Strict JWT Security**:
  - Implement `IAuthService` and `AuthController` with endpoints:
    - `POST /api/auth/login`: Validate credentials with BCrypt verification (`BCrypt.Net-Next` or standard HMAC/salt verification), verify active status (`is_active = true`), issue JWT Bearer Access Token (HMAC-SHA256, expiration ~30-60 min) containing `sub`/`userId`, `unique_name`/`username`, `role` (Admin/Manager/SalesStaff/WarehouseStaff), and `jti`.
    - Generate cryptographically secure random Refresh Token, hash it via SHA-256 (`token_hash`), and persist into `refresh_tokens` table with expiration (7 days).
    - `GET /api/auth/me`: Authorized via `[Authorize]`, validate JWT signature, return `UserProfileDto`.
    - `POST /api/auth/logout`: Authorized via `[Authorize]`, revoke the associated Refresh Token (`is_revoked = true`, `updated_at = now()`) in PostgreSQL database, and notify client to purge session storage.
- **Backend Store Settings API**: Implement `IStoreSettingsService` and `StoreSettingsController`:
  - `GET /api/settings/store`: Retrieve store profile (Store name, address, phone hotline, email, tax id, working hours).
  - `PUT /api/settings/store`: Update store profile with validation.
  - Persisted via `store_settings` table (or standard configuration record in DB/DbContext with initial seed).
- **C# Markup Foundation & Modern UI Integration**:
  - Add `Uno.Extensions.Markup.WinUI` to `frontend/Directory.Packages.props` and `TechStore.Client.csproj`.
  - Build modern, type-safe C# Markup layouts without raw repetitive markup, leveraging CommunityToolkit `SettingsCard` and `SettingsExpander`.
  - Convert `ShellPage.xaml` / `ShellPage.xaml.cs` to `ShellPage.cs` (C# Markup Fluent API) and delete legacy `.xaml` files.
  - Convert `SettingsPage.xaml` / `SettingsPage.xaml.cs` to `SettingsPage.cs` (C# Markup Fluent API) and delete legacy `.xaml` files.
  - Implement `LoginPage.cs` in C# Markup connecting to `LoginViewModel` and `ISessionStore`.
- **Testing**:
  - Add unit tests for `AuthService` (valid login, invalid password, inactive user, profile claims, logout revocation) and `StoreSettingsService` using xUnit and hand-written test doubles.
  - Add headless unit tests for `LoginViewModel` and `SettingsViewModel`.

**Non-Goals:**
- Creating POS sales orders or inventory balance tracking (assigned to Dev 3 & Dev 4).
- Creating catalog product management UI (assigned to Dev 2).
- Google OAuth or external SSO (in-store POS requires local username/password credentials).

## Decisions

### Decision 1: Authentication & Token Security Architecture
- **Choice**:
  1. **JWT Standard**: Use `Microsoft.AspNetCore.Authentication.JwtBearer` with HMAC-SHA256 signing (`SecurityAlgorithms.HmacSha256`). Signing secret stored in `JwtOptions` (min 256 bits).
  2. **Claims**: Standard claims (`ClaimTypes.NameIdentifier`, `ClaimTypes.Name`, `ClaimTypes.Role`, `JwtRegisteredClaimNames.Jti`).
  3. **Password Hashing**: BCrypt (`BCrypt.Net-Next` / RFC 2898 standard) to resist rainbow tables and brute force attacks.
  4. **Refresh Token Storage**: Refresh tokens are generated as high-entropy cryptographically random strings (`RandomNumberGenerator`), but only their SHA-256 hash (`TokenHash`) is stored in the `refresh_tokens` table. This prevents database leakage compromises.
  5. **Revocation & Logout**: Upon calling `POST /api/auth/logout`, the server marks `is_revoked = true` in PostgreSQL database. The client clears the Bearer token and refresh token from `ISessionStore`.
- **Rationale**: Complies with strict security standards for retail POS applications handling financial and sensitive data.
- **Alternatives Considered**: Opaque reference tokens without JWT — rejected because JWT allows stateless fast verification at backend controllers with minimal DB roundtrips for high-throughput POS queries.

### Decision 2: Store Settings Persistence
- **Choice**: Introduce a lightweight `StoreSetting` entity (or key-value/singleton record in `AppDbContext`) mapped to `store_settings` with seed data for TechStore.
- **Rationale**: Guarantees persistence across backend restarts and allows Store settings (`UC-STORE-01`, `UC-STORE-02`) to be dynamically configured by the store owner without code redeployment.

### Decision 3: C# Markup Adoption & Modern UI Library
- **Choice**: Use `Uno.Extensions.Markup.WinUI` alongside CommunityToolkit `SettingsControls` (`SettingsCard`, `SettingsExpander`).
- **Rationale**: Eliminates fragile string-based XAML, provides compile-time checking, and allows fluid composition of modern UI components for settings and forms instead of writing basic StackPanels and borders from scratch.
- **Legacy Cleanup**: Completely delete `ShellPage.xaml`, `ShellPage.xaml.cs`, `SettingsPage.xaml`, and `SettingsPage.xaml.cs` once their C# Markup equivalents `ShellPage.cs` and `SettingsPage.cs` are established.

### Decision 4: Navigation Flow & Login Experience
- **Choice**: Support both direct Login route and authenticated Shell pane profile indicator.
- **Rationale**: When unauthenticated, user can log in via `LoginPage`; upon login, token is saved to `ISessionStore`, navigation routes into Shell with full admin credentials displayed in pane footer.

## Risks / Trade-offs

- **[Risk]** Package conflicts between `Uno.Sdk` implicit packages and `Uno.Extensions.Markup.WinUI`.
  - **Mitigation**: Add package version centrally in `Directory.Packages.props` aligned with Uno SDK 6.7.x, test build with `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`.
- **[Risk]** Breaking existing headless tests that link `SettingsPage` or `ShellPage`.
  - **Mitigation**: ViewModels (`SettingsViewModel`, `ShellViewModel`) remain 100% headless POCOs in `TechStore.Client.UnitTests`. Views in C# Markup only depend on ViewModel properties via bindings.
