# Design

## Context

In `backend/Identity/`, `AuthService` and `StoreSettingsService` currently take direct dependencies on `AppDbContext`. Additionally, `AuthController` and `StoreController` reside in a nested `backend/Identity/Controllers/` directory, while all other backend modules (`Catalog`, `Inventory`, `Orders`) maintain controllers directly at the module root.
See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**
- Enforce the strict layering rule: `Controller → Service → Repository → AppDbContext`.
- Introduce repository abstractions in `backend/Identity/Repositories/` (`IUserRepository`, `IRefreshTokenRepository`, `IStoreSettingRepository`).
- Refactor `AuthService` and `StoreSettingsService` to depend only on these repositories and `IUnitOfWork`.
- Move `AuthController.cs` and `StoreController.cs` directly to `backend/Identity/`, eliminating the `Controllers/` subfolder.
- Convert unit tests for `AuthService` and `StoreSettingsService` to use hand-written fake repositories, adhering to the project's **Zero Dynamic Mocking** philosophy.

**Non-Goals:**
- Altering the database schema or creating EF Core migrations (the entities `User`, `Role`, `UserRole`, `RefreshToken`, and `StoreSetting` remain unchanged).
- Changing HTTP routes, request/response DTO schemas, or token generation algorithms.

## Decisions

### 1. Repository Boundary Design

- **`IUserRepository`**:
  - `Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);` (includes user roles and role definitions).
  - `Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken cancellationToken = default);`
  - `void Add(User user);`
- **`IRefreshTokenRepository`**:
  - `Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);`
  - `void Add(RefreshToken refreshToken);`
  - `void Remove(RefreshToken refreshToken);`
- **`IStoreSettingRepository`**:
  - `Task<StoreSetting?> GetCurrentSettingsAsync(CancellationToken cancellationToken = default);`
  - `void Add(StoreSetting setting);`

*Rationale*: Keeps repository interfaces minimal, expressive, and aligned with exact query needs of `AuthService` and `StoreSettingsService`. Repositories do not invoke `SaveChangesAsync` or manage transactions.

### 2. Controller Location & Namespace Conventions

- Move `backend/Identity/Controllers/AuthController.cs` → `backend/Identity/AuthController.cs` (namespace `TechStore.Api.Identity`).
- Move `backend/Identity/Controllers/StoreController.cs` → `backend/Identity/StoreController.cs` (namespace `TechStore.Api.Identity`).
- Matches `backend/Catalog/ProductsController.cs` and `backend/Orders/OrdersController.cs`.

### 3. Hand-written Fakes for Unit Tests

- Create `FakeUserRepository`, `FakeRefreshTokenRepository`, and `FakeStoreSettingRepository` in `tests/Backend.UnitTests/Fakes/`.
- Update `AuthServiceTests.cs` and `StoreSettingsTests.cs` to test pure business logic (bashing, JWT expiry, invalidation, validation) without spinning up EF Core In-Memory contexts.

## Risks / Trade-offs

- **[Namespace & Routing Impacts]** → Moving controllers could change their default routing if token replacement `[controller]` is affected by folder names.
  *Mitigation*: The routes are explicitly declared (`[Route("api/[controller]")]` for AuthController and `[Route("api/settings/[controller]")]` for StoreController), so moving files between folders within the same project has zero effect on routing.
- **[DI Lifetime Consistency]** → Register all new repositories as `Scoped` in `Program.cs`, matching the existing `IProductRepository` and `IOrderRepository`.

## Migration Plan

1. Create repository interfaces and EF Core implementations in `backend/Identity/Repositories/`.
2. Move controllers to `backend/Identity/` and delete empty folder.
3. Update `AuthService` and `StoreSettingsService` constructors and method calls.
4. Update DI registrations in `backend/Program.cs`.
5. Update unit tests with new Fake repositories.
6. Run `dotnet test TechStore.sln` to verify full regression-free functionality.
