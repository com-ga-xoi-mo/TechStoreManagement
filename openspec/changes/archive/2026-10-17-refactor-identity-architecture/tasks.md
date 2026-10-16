# Tasks

## 1. Create Identity Repositories

- [x] 1.1 Create `IUserRepository.cs` and `UserRepository.cs` in `backend/Identity/Repositories/` to encapsulate user and user role queries/staging.
- [x] 1.2 Create `IRefreshTokenRepository.cs` and `RefreshTokenRepository.cs` in `backend/Identity/Repositories/` for refresh token lookup, insertion, and invalidation.
- [x] 1.3 Create `IStoreSettingRepository.cs` and `StoreSettingRepository.cs` in `backend/Identity/Repositories/` for retrieving and staging store settings.

## 2. Refactor Identity Services & Controllers

- [x] 2.1 Refactor `AuthService.cs` in `backend/Identity/Services/` to inject `IUserRepository`, `IRefreshTokenRepository`, and `IUnitOfWork` instead of `AppDbContext`.
- [x] 2.2 Refactor `StoreSettingsService.cs` in `backend/Identity/Services/` to inject `IStoreSettingRepository` and `IUnitOfWork` instead of `AppDbContext`.
- [x] 2.3 Move `backend/Identity/Controllers/AuthController.cs` and `backend/Identity/Controllers/StoreController.cs` directly to `backend/Identity/`, remove the empty `backend/Identity/Controllers/` directory, and update namespaces.
- [x] 2.4 Register `IUserRepository`, `IRefreshTokenRepository`, and `IStoreSettingRepository` as scoped services in `backend/Program.cs`. Verify backend builds cleanly via `dotnet build backend/TechStore.Api.csproj`.

## 3. Update Unit Tests & Verification

- [x] 3.1 Create `FakeUserRepository.cs`, `FakeRefreshTokenRepository.cs`, and `FakeStoreSettingRepository.cs` in `tests/Backend.UnitTests/Fakes/`.
- [x] 3.2 Update `AuthServiceTests.cs` and `StoreSettingsTests.cs` to test services against the new fake repositories without `AppDbContext` dependencies.
- [x] 3.3 Run `dotnet test TechStore.sln` to verify all 470 unit tests pass and confirm zero regression.

## 4. Manual Test Scripts (cURL)

- [x] 4.1 Create an executable shell script `scripts/test-identity-endpoints.sh` containing cURL requests to manually verify all Identity endpoints against a running backend (`http://localhost:5176`):
  - `POST /api/auth/login` (Admin and Cashier authentication)
  - `GET /api/auth/me` (Profile retrieval with Bearer token)
  - `POST /api/auth/refresh` (Token refresh)
  - `GET /api/settings/store` (Public/Admin store settings retrieval)
  - `PUT /api/settings/store` (Admin-only store settings update)
  - `POST /api/auth/logout` (Session invalidation & token revocation)
- [x] 4.2 Verify script executes cleanly with formatted JSON responses and correct HTTP status code validations.
