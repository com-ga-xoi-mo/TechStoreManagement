# Proposal

## Why

Milestone 2 requires the Catalog and Inventory modules (Dev 3 - Bình 218) to deliver enterprise-grade REST APIs for product category management and inventory/Serial-IMEI tracking. While Milestone 1 established the domain entities (`Category`, `InventoryStock`, `SerialImei`, `InventoryMovement`) and database configurations, the business logic layer, repository queries, transactional orchestrations, and API endpoints are currently missing.

Building these capabilities now is essential for unblocking downstream modules: the POS checkout flow, purchase order intake, and client desktop applications (Uno Platform) require real-time category trees, live stock availability, and serial/IMEI barcode lifecycle validation. Furthermore, strict ACID transactional guarantees, row-level locking (`FOR UPDATE`), and conflict handling must be implemented to uphold the system's anti-overselling invariants and audit trail requirements.

## What Changes

This change implements full-stack backend capabilities (Repositories, Services, Controllers, and Shared Contracts) across 6 core use cases for Dev 3:

1. **Category Management (`backend/Catalog/`)**:
   - **UC-CAT-01: Get Category Hierarchy Tree (`GET /api/v1/categories/tree`)**:
     - Returns active categories structured as a hierarchical tree (root categories with nested child categories).
     - Allows clients to render nested navigation menus and category pickers without flat client-side reconstruction.
   - **UC-CAT-02: Create Category (`POST /api/v1/categories`)**:
     - Creates a new product category with unique slug generation/validation and optional `ParentId` verification.
     - Role-based authorization: restricted to `Admin` and `Manager`.
     - Returns HTTP 201 Created on success, HTTP 400 Bad Request on validation error, and HTTP 409 Conflict if the slug already exists.

2. **Inventory Stock Management (`backend/Inventory/`)**:
   - **UC-INV-01: Query Aggregated Stocks (`GET /api/v1/inventory/stocks`)**:
     - Retrieves paginated inventory balances with flexible filters: `ProductId`, `VariantId`, and `LowStock` alert flag (`quantity <= min_stock_alert`).
     - Returns `PagedResultDto<InventoryStockDto>` with product and variant metadata.
   - **UC-INV-02: Initialize Initial Stock Balance (`POST /api/v1/inventory/stocks/initialize`)**:
     - Sets initial inventory balance for non-serial tracked or baseline items.
     - Enforces PostgreSQL row uniqueness on `(product_id, variant_id)` using `UNIQUE NULLS NOT DISTINCT`.
     - Runs inside an explicit `IUnitOfWork` ACID transaction.
     - Writes an append-only audit record to `inventory_movements` with `MovementType = OpeningBalance`, recording `QuantityChange`, `QuantityAfter`, and `PerformedByUserId`.
     - Role-based authorization: restricted to `Admin`, `Manager`, `WarehouseStaff`.

3. **Serial & IMEI Lifecycle Tracking (`backend/Inventory/`)**:
   - **UC-IMEI-01: Query Serial/IMEI List (`GET /api/v1/inventory/serials`)**:
     - Paginated lookup of specific physical hardware items filtered by `ProductId`, `VariantId`, and `Status` (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`).
     - Returns `PagedResultDto<SerialImeiDto>` including warranty duration and purchase order linkage.
   - **UC-IMEI-02: Batch Import Initial Serial/IMEI (`POST /api/v1/inventory/serials/batch-import`)**:
     - Imports a batch of serial numbers or IMEI strings for serialized products.
     - Validates IMEI format (15 digits) and non-empty identifiers per database constraints.
     - Performs duplicate checking across in-memory batch inputs and existing database records (`idx_serial_imeis_serial`, `idx_serial_imeis_imei`).
     - If duplicates exist, throws a domain exception or translates Postgres 23505 duplicate key violation to return HTTP 409 Conflict with RFC 7807 ProblemDetails identifying the duplicate values.
     - Atomically updates or creates the corresponding `inventory_stocks` row balance within an explicit ACID transaction using row-level locking (`FOR UPDATE`).
     - Synchronizes `quantity` to maintain the invariant: `quantity = COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.
     - Writes audit records to `inventory_movements` with `MovementType = OpeningBalance`.
     - Role-based authorization: restricted to `Admin`, `Manager`, `WarehouseStaff`.

4. **Shared Contracts & Layering Architecture**:
   - Adds DTOs (`CategoryDto`, `CategoryTreeNodeDto`, `InventoryStockDto`, `SerialImeiDto`) in `shared/DTOs/`.
   - Adds Requests (`CreateCategoryRequest`, `InventoryStockFilterRequest`, `InitializeStockRequest`, `SerialImeiFilterRequest`, `BatchImportSerialImeiRequest`) in `shared/Requests/`.
   - Strictly preserves 4-tier layer boundary: `Controller -> Service -> Repository -> AppDbContext`.
   - Services manage transactions via `IUnitOfWork` (`BeginTransactionAsync`, `SaveChangesAsync`, `CommitAsync`, `RollbackAsync`).

## Capabilities

### New Capabilities
- `category-catalog`: Category hierarchy tree generation, category persistence, slug uniqueness validation, and hierarchical parent verification.
- `inventory-management`: Stock balance querying with low-stock alerts, initial stock initialization, Serial/IMEI tracking, batch import with duplicate conflict detection, and transactional stock balance synchronization with movement ledger auditing.

### Modified Capabilities
*(None. Existing database schemas, entities, and configurations from Milestone 1 are reused without modifications.)*

## Impact

- **Backend (`backend/`)**:
  - `backend/Catalog/Repositories/`: Extend `ICategoryRepository` and `CategoryRepository` with tree fetching, slug checking, and add operations.
  - `backend/Catalog/Services/`: Add `ICategoryService` and `CategoryService` (with validation and tree building logic).
  - `backend/Catalog/CategoriesController.cs`: Expose `GET /api/v1/categories/tree` and `POST /api/v1/categories`.
  - `backend/Inventory/Repositories/`: Add `IInventoryStockRepository`, `InventoryStockRepository`, `ISerialImeiRepository`, and `SerialImeiRepository`.
  - `backend/Inventory/Services/`: Add `IInventoryService`, `InventoryService`, `ISerialImeiService`, `SerialImeiService`.
  - `backend/Inventory/InventoryController.cs`: Expose `/api/v1/inventory/stocks*` and `/api/v1/inventory/serials*`.
  - `backend/Program.cs`: Register new repositories and services in DI container.
- **Shared Contracts (`shared/`)**:
  - New DTO records in `shared/DTOs/`.
  - New Request records in `shared/Requests/`.
- **API Surface**:
  - `GET /api/v1/categories/tree`
  - `POST /api/v1/categories`
  - `GET /api/v1/inventory/stocks`
  - `POST /api/v1/inventory/stocks/initialize`
  - `GET /api/v1/inventory/serials`
  - `POST /api/v1/inventory/serials/batch-import`
- **Tests (`tests/Backend.UnitTests/`)**:
  - Unit tests for category tree building and validation.
  - Unit tests for inventory stock calculations, duplicate serial detection, and transaction orchestration with fake repository and unit of work.
