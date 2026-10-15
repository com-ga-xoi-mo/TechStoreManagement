# Proposal

## Why
Sprint 2 requires Dev 1 (Lead) to implement Feature 1: System Administration & Authentication (`UC-AUTH-01..03` and `UC-STORE-01..02`), bootstrap the C# Markup (`Uno.Extensions.Markup`) foundation with modern UI libraries (WinUI CommunityToolkit SettingsControls, C# Markup styling), convert existing Shell and Settings views from XAML to type-safe C# Markup while removing legacy XAML files, and deliver the Admin Login screen.

## What Changes
- **Backend Auth & Store Endpoints**:
  - Implement `IAuthService` and `AuthController` (`POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`) with JWT token generation and role verification (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`).
  - Implement `IStoreSettingsService` and `StoreSettingsController` (`GET /api/settings/store`, `PUT /api/settings/store`) for system store profile management (name, hotlines, address, tax code, operating hours).
- **Shared Contracts**:
  - Add DTOs and Requests for authentication (`LoginRequest`, `LoginResponseDto`, `UserProfileDto`) and store settings (`StoreSettingsDto`, `UpdateStoreSettingsRequest`).
- **Frontend Uno Platform & C# Markup Integration**:
  - Configure `Uno.Extensions.Markup.WinUI` in `Directory.Packages.props` and `frontend/TechStore.Client/TechStore.Client.csproj` with modern UI library controls (`CommunityToolkit.WinUI.Controls.SettingsControls`).
  - Convert `ShellPage.xaml` / `ShellPage.xaml.cs` to 100% C# Markup `ShellPage.cs` with active session user status, responsive NavigationView, and remove legacy `ShellPage.xaml` / `ShellPage.xaml.cs`.
  - Convert `SettingsPage.xaml` / `SettingsPage.xaml.cs` to 100% C# Markup `SettingsPage.cs` integrating both Theme/System preferences and Store Settings management using `SettingsCard` / `SettingsExpander`, and remove legacy `SettingsPage.xaml` / `SettingsPage.xaml.cs`.
  - Create new C# Markup `LoginPage.cs` with modern UI design, validation, and session integration.
  - Implement `IAuthRepository` and `IStoreSettingsRepository` in frontend client, wiring `ISessionStore` with JWT bearer tokens.
- **Unit Tests**:
  - Add backend tests for `AuthService` (`UC-AUTH-01..03`) and `StoreSettingsService` (`UC-STORE-01..02`).
  - Add frontend tests for `LoginViewModel` and updated `SettingsViewModel`.

## Capabilities

### New Capabilities
- `admin-authentication`: Handles Admin and staff authentication, login verification, profile retrieval, logout token revocation, and client session state.
- `store-settings`: Handles store profile configuration (name, address, hotline, tax ID) both in backend persistent storage and frontend administrative settings view.

### Modified Capabilities
- `frontend-shell-navigation`: Convert Shell view to C# Markup (`ShellPage.cs`), integrate logged-in user profile badge in NavigationView pane footer, and route authentication state transitions.
- `frontend-design-system`: Establish C# Markup Fluent styling helpers and integrate CommunityToolkit SettingsControls for modern UI without raw boilerplate.

## Impact
- **Backend API**: New controllers `AuthController` and `StoreSettingsController`, entity configurations if needed for store settings, integration with existing `Identity` module (`User`, `Role`, `RefreshToken`).
- **Shared Contracts**: New requests and DTOs in `shared/DTOs/` and `shared/Requests/`.
- **Frontend Client**: Package reference to `Uno.Extensions.Markup.WinUI`, conversion of Shell and Settings to C# Markup, deletion of old `.xaml` files, new `LoginPage`.
- **Tests**: New unit tests in `tests/Backend.UnitTests/` and `tests/Frontend.UnitTests/`.
