# Proposal

## Why

Currently, `backend/Identity/` violates the backend layering architecture (`Controller → Service → Repository → AppDbContext`) outlined in `AGENTS.md`. Specifically:
1. `AuthService` and `StoreSettingsService` inject and query `AppDbContext` directly rather than through dedicated repository abstractions.
2. The `Identity` module places controllers in a nested `backend/Identity/Controllers/` subdirectory, whereas all other domain modules (`Catalog`, `Inventory`, `Orders`) place their controllers directly in the module root (`ProductsController.cs`, `InventoryController.cs`, `OrdersController.cs`).
3. Unit tests for `AuthService` and `StoreSettingsService` instantiate `AppDbContext` (in-memory) instead of utilizing hand-written fake repositories, deviating from the repository test isolation standard.

Refactoring `backend/Identity/` to include dedicated repositories (`IUserRepository`, `IRefreshTokenRepository`, `IStoreSettingRepository`) and aligning controller placement directly under `backend/Identity/` ensures 100% architectural consistency and enables clean unit testing with fake repositories.

## What Changes

- **Introduce Identity Repositories**:
  - Add `backend/Identity/Repositories/IUserRepository.cs` and `UserRepository.cs` for querying and staging user and role data.
  - Add `backend/Identity/Repositories/IRefreshTokenRepository.cs` and `RefreshTokenRepository.cs` for managing refresh token records.
  - Add `backend/Identity/Repositories/IStoreSettingRepository.cs` and `StoreSettingRepository.cs` for fetching and updating store settings.
- **Refactor Identity Services**:
  - Refactor `AuthService.cs` to depend only on `IUserRepository`, `IRefreshTokenRepository`, `IUnitOfWork`, and `TimeProvider`, completely removing `AppDbContext` injection.
  - Refactor `StoreSettingsService.cs` to depend only on `IStoreSettingRepository`, `IUnitOfWork`, and `TimeProvider`, removing `AppDbContext` injection.
- **Align Controller Placement**:
  - Move `backend/Identity/Controllers/AuthController.cs` to `backend/Identity/AuthController.cs`.
  - Move `backend/Identity/Controllers/StoreController.cs` to `backend/Identity/StoreController.cs`.
  - Remove empty `backend/Identity/Controllers/` directory.
- **Register Dependencies**:
  - Register new repositories (`IUserRepository`, `IRefreshTokenRepository`, `IStoreSettingRepository`) as scoped services in `backend/Program.cs`.
- **Update Unit Tests**:
  - Introduce `FakeUserRepository`, `FakeRefreshTokenRepository`, and `FakeStoreSettingRepository` in `tests/Backend.UnitTests/Fakes/`.
  - Update `AuthServiceTests.cs` and `StoreSettingsTests.cs` to test services against fake repositories without database dependencies.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
<!-- None: This is a pure backend architectural refactoring preserving exact HTTP endpoints, status codes, and business behavior. -->

## Impact

- **Affected Code**:
  - `backend/Identity/` (Services, Controllers, and new Repositories)
  - `backend/Program.cs` (DI registrations)
  - `tests/Backend.UnitTests/` (Service unit tests and Fakes)
- **APIs & Contracts**: Zero breaking changes. `POST /api/auth/login`, `POST /api/auth/refresh`, `GET /api/auth/me`, `POST /api/auth/logout`, `GET /api/settings/store`, and `PUT /api/settings/store` retain identical contracts, routes, and responses.
