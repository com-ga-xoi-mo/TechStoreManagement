# Spec Delta

## Purpose

Lets warehouse staff keep the list of suppliers that goods are bought from, so every purchase receipt names where its stock came from.

## ADDED Requirements

### Requirement: Supplier listing
The API SHALL expose `GET /api/inventory/suppliers` to Admin, Manager and WarehouseStaff, returning a page of suppliers sorted by name with `search` (name, contact name, phone or email, case-insensitive, `%` and `_` literal) and `isActive` filters. The system supplier "Số dư đầu kỳ" SHALL never appear in the list.

#### Scenario: Search suppliers
- **WHEN** a WarehouseStaff user requests `/api/inventory/suppliers?search=fpt&isActive=true`
- **THEN** the API returns HTTP 200 with `PagedResultDto<SupplierDto>` containing only active suppliers whose name, contact name, phone or email contains "fpt", each with id, name, contact name, phone, email, address, `isActive` and receipt count

#### Scenario: System supplier hidden
- **WHEN** any allowed role lists suppliers without filters
- **THEN** the result does not contain the supplier "Số dư đầu kỳ"

#### Scenario: Listing denied
- **WHEN** an unauthenticated caller or a SalesStaff user requests the list
- **THEN** the API returns HTTP 401 or HTTP 403 respectively

### Requirement: Supplier creation and update
The API SHALL let Admin and WarehouseStaff create a supplier with `POST /api/inventory/suppliers` and update it with `PUT /api/inventory/suppliers/{id}`. Name (1–200 characters after trimming) and phone (1–30 characters, digits, spaces, `+`, `-`, `.`, parentheses) SHALL be required; contact name and address SHALL be at most 200 and 500 characters; email SHALL be optional and well formed. A name equal, ignoring case and surrounding spaces, to another supplier's name SHALL be refused.

#### Scenario: Create a supplier
- **WHEN** a WarehouseStaff user posts name "FPT Trading", phone "028 7300 7300" and email "sales@fpt.example"
- **THEN** the API returns HTTP 201 with the new active supplier and a Location header `/api/inventory/suppliers/{id}`

#### Scenario: Invalid supplier fields
- **WHEN** the request has an empty name, a missing phone, a phone with letters, an email without `@` or a field above its length limit
- **THEN** the API returns HTTP 400 ValidationProblem keyed by field and saves nothing

#### Scenario: Duplicate supplier name
- **WHEN** a supplier "FPT Trading" exists and a request creates or renames another supplier to " fpt trading "
- **THEN** the API returns HTTP 409 ProblemDetails and saves nothing

#### Scenario: Update unknown supplier
- **WHEN** `PUT /api/inventory/suppliers/{id}` targets an id that does not exist
- **THEN** the API returns HTTP 404 ProblemDetails

#### Scenario: Write denied by role
- **WHEN** a Manager or SalesStaff user calls create or update
- **THEN** the API returns HTTP 403 and saves nothing

### Requirement: Supplier deactivation
The API SHALL let Admin and WarehouseStaff stop or resume using a supplier with `PATCH /api/inventory/suppliers/{id}/status` and body `{ "isActive": bool }`. Deactivation SHALL keep the supplier and all its purchase receipts; an inactive supplier SHALL NOT be accepted on a new receipt. Suppliers SHALL never be deleted through the API.

#### Scenario: Deactivate a supplier with history
- **WHEN** a WarehouseStaff user deactivates a supplier that has receipts
- **THEN** the API returns HTTP 200 with `isActive = false`, and its existing receipts still show that supplier

#### Scenario: Resume a supplier
- **WHEN** an Admin sets `isActive = true` on an inactive supplier
- **THEN** the API returns HTTP 200 and the supplier can be chosen on new receipts again

### Requirement: System supplier protection
The supplier "Số dư đầu kỳ" that holds opening-balance purchase orders SHALL NOT be updated, activated or deactivated through the API, and no other supplier SHALL be created with or renamed to that name, ignoring case and surrounding spaces.

#### Scenario: Modify the system supplier
- **WHEN** a caller sends update or status change for the "Số dư đầu kỳ" supplier id
- **THEN** the API returns HTTP 409 ProblemDetails and changes nothing

#### Scenario: Reserved name
- **WHEN** a caller creates a supplier named "số dư đầu kỳ" before any opening balance exists
- **THEN** the API returns HTTP 409 ProblemDetails and saves nothing
