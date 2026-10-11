# Tasks

## 1. Branch and planning artifacts

- [x] 1.1 Create branch `docs/repository-unit-of-work` from `feat/catalog-product-api` (or from `dev` if that PR is already merged); verify `git branch --show-current` prints `docs/repository-unit-of-work` and keep the pre-existing `.gitignore` edit out of every commit
- [x] 1.2 Commit `openspec/changes/repository-unit-of-work-convention/` as `docs(openspec): record repository-unit-of-work-convention change artifacts`; verify `git show --stat HEAD` lists only files under that folder

## 2. `AGENTS.md` (English)

- [x] 2.1 Key Modules (`backend/Common/` bullet, around line 51): mention `IUnitOfWork` and `DuplicateKeyException`; each module bullet keeps its services/controllers and states data access goes through its repositories; verify by reading the section
- [x] 2.2 Data Flow (section 3, steps 4–6): Controller → Service (business rules, opens transaction via `IUnitOfWork`) → Repository (EF Core/SQL, row locks) → `IUnitOfWork.SaveChangesAsync`/commit → PostgreSQL; verify no step says the service uses `AppDbContext`
- [x] 2.3 Key Directories tree: add `Repositories/` under the backend module description and `Common/Data` items (`AppDbContext`, `IUnitOfWork`, `DuplicateKeyException`); verify the tree stays aligned
- [x] 2.4 Add a "Layering & Repository Pattern" subsection under Code Conventions describing design D1, D2, D3, D4 and D6 (layer rule, repository per aggregate, intent methods, returns entities never DTOs/`IQueryable`, no generic repository, criteria records, `IUnitOfWork` owns `SaveChanges`/transactions, `DuplicateKeyException`, cross-module reads only); verify it does not exceed one screen and contains no implementation code beyond short signatures
- [x] 2.5 Rewrite section 4 "Concurrency & Anti-Overselling Pattern" with the `IUnitOfWork` + `GetForUpdateAsync` example from design D5 and the rule that lock methods and direct-write methods (`ExecuteUpdateAsync` / `ExecuteDeleteAsync` / raw write SQL) require an open transaction; verify `grep -n "_dbContext" AGENTS.md` returns no service-level example
- [x] 2.6 Section 6 DI: list repositories and `IUnitOfWork` as Scoped (replace the vague "unit-of-work services"); naming table: add `I<Aggregate>Repository` / `<Aggregate>Repository` rows and change the private-field example to `_productRepository`, `_unitOfWork`; verify the table renders
- [x] 2.7 Testing Conventions: add that services are unit-tested with hand-written fake repositories and a fake `IUnitOfWork`, no mocking library; verify the bullet sits with the other testing conventions

## 3. `docs/ARCHITECTURE.md` (Vietnamese)

- [x] 3.1 Section 1 backend box: add a line for "Service → Repository → Unit of Work" layering; verify the box border stays aligned (ASCII/box-drawing width)
- [x] 3.2 Section 2.2: add `Repositories/` with planned repository names to every module (Catalog: `IProductRepository` (Dev 2), `ICategoryRepository` (Dev 3); Inventory: `IInventoryStockRepository`, `ISerialImeiRepository`, `IPurchaseOrderRepository`, `ISupplierRepository`; Orders: `IOrderRepository`; Customers: `ICustomerRepository`, `IVoucherRepository`; Sales: `IPosSessionRepository`, `IVietQrTransactionRepository`; Identity: `IUserRepository`, `IRefreshTokenRepository`) and `IUnitOfWork`, `UnitOfWork`, `DuplicateKeyException` under `Common/Data/`; verify the tree stays aligned
- [x] 3.3 Add a short subsection (after 2.2) "Phân tầng Backend: Controller → Service → Repository → Unit of Work" in Vietnamese summarizing D1–D6 with an ASCII diagram; verify it is consistent with `AGENTS.md`
- [x] 3.4 Concurrency notes in section 3.3 (giữ chỗ / `FOR UPDATE`): state the lock is a repository method executed inside a transaction opened by the service via `IUnitOfWork`; verify wording matches design D5

## 4. Module READMEs (Vietnamese)

- [x] 4.1 `backend/Catalog/README.md` section 3: add `Repositories/` (`IProductRepository`/`ProductRepository` – Dev 2, `ICategoryRepository`/`CategoryRepository` – Dev 3) and a note that `ProductService` will be refactored onto `IProductRepository` in a follow-up change; verify the section lists Configurations, Repositories, Services, controllers in that order
- [x] 4.2 `backend/Inventory/README.md`, `backend/Orders/README.md`, `backend/Customers/README.md`, `backend/Sales/README.md`, `backend/Identity/README.md`: add a `Repositories/` entry with the planned names from 3.2, and replace the `Controllers/` folder entry with "controller files at the module root" (matching `docs/ARCHITECTURE.md`); in Inventory also state that `FOR UPDATE` lock methods live in `IInventoryStockRepository` and require an `IUnitOfWork` transaction; verify `grep -n "Controllers/\`" backend/*/README.md` returns nothing

## 5. Consistency check and commit

- [x] 5.1 Verify consistency: `grep -rn "_dbContext.Database.BeginTransactionAsync" AGENTS.md docs/ backend/*/README.md` returns nothing; every module in `docs/ARCHITECTURE.md` 2.2 has a `Repositories/` line; repository names match between `docs/ARCHITECTURE.md` and the module READMEs
- [x] 5.2 Commit the documentation as `docs: adopt repository and unit-of-work convention across backend modules` (only `AGENTS.md`, `docs/ARCHITECTURE.md`, `backend/*/README.md`); verify with `git show --stat HEAD`
- [x] 5.3 Commit updated `tasks.md` as `docs(openspec): update repository-unit-of-work-convention task progress`; verify `git status --short` shows only the pre-existing `.gitignore` edit

## Workflow follow-up

- Open a pull request into `dev` and ask Dev 1, Dev 3 and Dev 4 to review, since the convention applies to their modules.
- Follow-up code changes (separate proposals): Dev 1 implements `IUnitOfWork` / `UnitOfWork` / `DuplicateKeyException`; Dev 2 refactors `ProductService` onto `IProductRepository` with fake-based unit tests.
- Archive this change after merge.
