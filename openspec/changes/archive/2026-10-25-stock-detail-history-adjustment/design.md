# Design

## Context

The inventory list already loads `InventoryStockDto` through the bearer-token API client and renders visibility/edit actions in a C# Markup page. The backend owns `inventory_stocks` and the append-only `inventory_movements` ledger, while `IUnitOfWork` provides explicit transaction boundaries. The two HTML references define the modal layout and visual hierarchy; AGENTS.md requires C# Markup, MVVM-only presentation logic, and no client database access.

## Goals / Non-Goals

**Goals:**

- Add a read model that combines stock, product/category, serial count, and the latest ten movements.
- Apply quantity adjustments under a row lock and preserve `Quantity >= ReservedQuantity >= 0`.
- Keep dialogs headless-testable through ViewModels and normalized API results.
- Reuse the existing dialog presenter, design tokens, and inventory refresh/toast path.

**Non-Goals:**

- Serial/IMEI detail management itself; the Serial/IMEI link remains a hook for its later workflow.
- New warehouse selection, purchase receiving, or stock initialization forms.
- A database migration for a separate movement-note column; the existing reason column remains the persistence seam.

## Decisions

### 1. Separate detail and adjustment contracts

Use `InventoryStockDetailDto`, `InventoryMovementDto`, and `AdjustStockRequest` rather than extending the paged row DTO with a variable history payload. This keeps list responses small and makes the detail endpoint independently cacheable. An alternative was to return movements from `GET /stocks`, but that would increase every page response and couple the table to audit history.

### 2. Service-owned transaction and lock

`AdjustStockAsync` starts a unit-of-work transaction, obtains the stock row with `SELECT ... FOR UPDATE`, validates the proposed quantity against reservations, updates the balance, appends an Adjustment movement, saves, and commits. The controller remains a thin authorization/HTTP mapping layer. An application-only lock was rejected because it cannot protect concurrent API instances.

### 3. Role enforcement at the endpoint

Detail retrieval uses the controller's authenticated-staff policy. Adjustment uses the explicit `Admin,WarehouseStaff` role policy, with the service still validating the request and user identity. This provides early 403 responses while preserving domain invariants if the service is called from another entry point.

### 4. ViewModel-driven modal flow

`InventoryStockViewModel` emits typed row events. The page creates `StockDetailHistoryDialogViewModel` or `StockAdjustmentDialogViewModel`, presents the corresponding C# Markup `ContentDialog` through `ContentDialogPresenter`, and refreshes only after a success event. Draft validation and live projection stay in the adjustment ViewModel; controls never call the API directly.

### 5. Existing movement reason as note seam

The request accepts `Reason` and `Note`; until the schema has a dedicated note column, the service stores the two values as one reason string and maps it back through the detail DTO's `Note`. A future migration can split the fields without changing the HTTP contract.

## Risks / Trade-offs

- **[Existing schemas have no dedicated note column]** → Preserve the combined reason representation and document it; add a migration only when the database contract is intentionally versioned.
- **[Filtered movement history can change while a dialog is open]** → Reload detail on dialog open and treat the post-adjustment detail/list refresh as authoritative.
- **[Presenter/root may be unavailable during navigation]** → Use the existing presenter readiness check and active `XamlRoot` fallback, returning without a mutation when no owner exists.
- **[API callers can submit very large signed changes]** → Validate non-zero input, enforce non-negative/reservation-safe quantities, and reject integer overflow as a conflict.

## Migration Plan

1. Deploy shared contracts, backend endpoints, service/repository changes, and tests.
2. Deploy the client; existing list and delete workflows remain compatible.
3. Monitor 400/403/409 adjustment responses and ledger entries.
4. Roll back the client independently if needed; rollback the API only after confirming no client is issuing adjustment requests. No schema migration is required for this version.
