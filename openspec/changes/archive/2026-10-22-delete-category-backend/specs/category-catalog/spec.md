# Spec Delta

## ADDED Requirements

### Requirement: Safe Empty Category Deletion
The system SHALL delete a category only when it exists, has no direct subcategories, and has no associated products. A successful deletion MUST return HTTP 204 with no response body.

#### Scenario: Delete an empty category successfully
- **WHEN** an authenticated Admin sends `DELETE /api/categories/{id}` for an existing category with no direct children and no associated products
- **THEN** the system removes the category and returns HTTP 204 No Content

#### Scenario: Target category does not exist
- **WHEN** an authenticated Admin sends `DELETE /api/categories/{id}` for an ID that is absent from the database
- **THEN** the system returns HTTP 404 with a ProblemDetails response and does not persist a deletion

#### Scenario: Deletion is persisted atomically
- **WHEN** a deletion passes all integrity checks
- **THEN** the system performs the checks and deletion within the service-owned unit-of-work transaction and reports success only after persistence completes

### Requirement: Subcategory Deletion Protection
The system MUST reject deletion when the target category has one or more direct subcategories and MUST preserve the category and its hierarchy.

#### Scenario: Category has direct subcategories
- **WHEN** an authenticated Admin attempts to delete a category that contains at least one direct subcategory
- **THEN** the system returns HTTP 400 with the message `Cannot delete category that contains subcategories. Move or delete subcategories first.` and does not delete the target

### Requirement: Associated Product Deletion Protection
The system MUST reject deletion when any product is associated with the target category and MUST preserve the category and its product association.

#### Scenario: Category has associated products
- **WHEN** an authenticated Admin attempts to delete a category referenced by one or more products
- **THEN** the system returns HTTP 409 with the message `Cannot delete category that contains associated products. Reassign or delete associated products first.` and does not delete the target

### Requirement: Category Deletion Authorization and Cancellation
The deletion endpoint SHALL require a valid JWT bearer token with the Admin role. The system MUST propagate request cancellation through pending database work and roll back any uncommitted deletion.

#### Scenario: Unauthenticated deletion
- **WHEN** a caller without a valid token sends `DELETE /api/categories/{id}`
- **THEN** the API returns HTTP 401 before category mutation occurs

#### Scenario: Non-admin deletion
- **WHEN** an authenticated Manager, SalesStaff, or WarehouseStaff sends `DELETE /api/categories/{id}`
- **THEN** the API returns HTTP 403 before category mutation occurs

#### Scenario: Admin deletion
- **WHEN** an authenticated Admin sends a valid deletion request
- **THEN** authorization permits the deletion workflow to execute

#### Scenario: Request cancellation
- **WHEN** the request is cancelled while the deletion is waiting on integrity checks or persistence
- **THEN** cancellation propagates to repository and unit-of-work operations, any open transaction is rolled back, and the API does not report a successful deletion
