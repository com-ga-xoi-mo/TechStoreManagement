# Tasks

## 1. Shared Contracts & Backend Identity/Store Setup

- [x] 1.1 Define Auth & Store DTOs and Requests in `shared/` (`LoginRequest`, `LoginResponseDto`, `UserProfileDto`, `StoreSettingsDto`, `UpdateStoreSettingsRequest`) and verify shared project compiles cleanly.
- [x] 1.2 Implement Store Settings persistence and entity (`StoreSetting`) with EF Core configuration in `backend/Identity/` or `backend/Common/` and verify database migration and schema build.
- [x] 1.3 Implement `IAuthService`, `AuthService`, and `AuthController` (`UC-AUTH-01`, `UC-AUTH-02`, `UC-AUTH-03`) with secure JWT generation (HMAC-SHA256, standard claims), BCrypt password hashing, SHA-256 hashed Refresh Tokens, and revocation on logout, verifying with unit tests in `AuthServiceTests.cs`.
- [x] 1.4 Implement `IStoreSettingsService`, `StoreSettingsService`, and `StoreSettingsController` (`UC-STORE-01`, `UC-STORE-02`), verifying with unit tests in `StoreSettingsTests.cs`.

## 2. Uno Platform C# Markup & Modern UI Foundation (Stitch Design System Alignment)

- [x] 2.1 Add `Uno.Extensions.Markup.WinUI` to `frontend/Directory.Packages.props` and `TechStore.Client.csproj`, verifying `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop` passes.
- [x] 2.2 Create modern UI helper classes and styling extensions for C# Markup and CommunityToolkit Controls mirroring exact Stitch design tokens from `stitch_techstore_retail_admin_dashboard/techstore_design_system/` (Colors: Primary `#0066FF`, Surface `#F8FAFC`/`#FFFFFF`, Border `#E2E8F0`, Typography: Inter / Plus Jakarta Sans, Radius: `8px`/`12px`/`16px`), verifying build without warnings.
- [x] 2.3 Implement frontend API clients and repositories: `IAuthRepository`/`AuthRepository` and `IStoreSettingsRepository`/`StoreSettingsRepository` connected to `ISessionStore` with secure JWT bearer propagation.

## 3. Convert Views to C# Markup & Implement Admin Screens Exact to Stitch

- [x] 3.1 Convert `ShellPage.xaml` / `ShellPage.xaml.cs` to 100% C# Markup `ShellPage.cs` matching `stitch_techstore_retail_admin_dashboard/admin_dashboard_techstore_management_system/` layout (Sidebar, Header, active user profile badge in pane footer), and delete legacy `ShellPage.xaml` / `ShellPage.xaml.cs`.
- [x] 3.2 Convert `SettingsPage.xaml` / `SettingsPage.xaml.cs` to 100% C# Markup `SettingsPage.cs` accurately implementing the Stitch design in `stitch_techstore_retail_admin_dashboard/c_i_t_tab_1_c_a_h_ng_l_u_thay_i/` (Tab bar, Store Settings form card max-w 680px, labels with red asterisk, success banner `#ECFDF5`/`#065F46`, save button `#0066FF`), and delete legacy `SettingsPage.xaml` / `SettingsPage.xaml.cs`.
- [x] 3.3 Create modern `LoginPage.cs` in C# Markup matching 100% pixel-accurate layout from `stitch_techstore_retail_admin_dashboard/ng_nh_p_techstore_management_system/` (Left branding panel with gradient `#0B1528` - `#0A2558`, Right login form card with user/lock icons, password visibility toggle, error state banners), with `LoginViewModel` integrating authentication, validation, error handling, and secure session state.
- [x] 3.4 Wire up DI in `App.xaml.cs` / `UiServiceCollectionExtensions.cs` and verify routes for Login, Shell, and Settings.

## 4. Verification and Automated Testing

- [x] 4.1 Implement frontend unit tests for `LoginViewModel` and `SettingsViewModel` in `tests/Frontend.UnitTests/` and verify all tests pass.
- [x] 4.2 Run full solution build (`dotnet build TechStore.sln`) and test suite (`dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` and `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`), verifying 100% pass rate.
