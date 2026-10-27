# Spec Delta

## ADDED Requirements

### Requirement: Administrative Category Update
The system SHALL allow an authenticated Admin to update an existing category through `PUT /api/categories/{id}` and return HTTP 200 with its complete category representation, including the five editable fields and timestamps. The identifier and creation timestamp MUST remain unchanged; each successful update MUST assign the current UTC time to `UpdatedAt`.

#### Scenario: Replace editable category information
- **GIVEN** an existing category and a valid parent outside its subtree
- **WHEN** an Admin submits valid `Name`, `Slug`, `Description`, `ParentId`, and `IsActive`
- **THEN** all five values are persisted and returned with the original `Id` and `CreatedAt` and a fresh UTC `UpdatedAt`
- **AND** leading and trailing whitespace is removed from name and non-null description

#### Scenario: Clear description with empty string
- **WHEN** an Admin updates a category with an empty string or whitespace-only `Description`
- **THEN** the description is cleared to null

#### Scenario: Preserve existing description when omitted or null
- **WHEN** an Admin updates a category with null or omitted `Description`
- **THEN** the existing description is retained without modification

#### Scenario: Remove parent to make root
- **WHEN** an Admin updates a category with null `ParentId`
- **THEN** the category parent is cleared, making the category a root

#### Scenario: Explicit active state is required
- **WHEN** the update payload omits `IsActive` or supplies null
- **THEN** HTTP 400 is returned with an `isActive` field error and no update is persisted

#### Scenario: Missing target category
- **WHEN** an Admin updates a well-formed category ID that does not exist
- **THEN** HTTP 404 with a ProblemDetails response is returned and no category is created

### Requirement: Category Update Name Validation
The system SHALL reject updates whose trimmed name is missing, empty, whitespace-only, or longer than 200 characters, returning HTTP 400 with field-level errors and persisting no changes.

#### Scenario: Invalid name
- **WHEN** a category update provides a null, empty, or whitespace-only name, or a trimmed name of 201 characters
- **THEN** the response contains a `name` validation error and the category remains unchanged

#### Scenario: Boundary length name
- **WHEN** an otherwise valid update provides a trimmed name of exactly 200 characters
- **THEN** name validation succeeds

### Requirement: Category Update Slug Normalization
The system SHALL trim and lowercase supplied nonblank slugs and require the result to match `^[a-z0-9]+(?:-[a-z0-9]+)*$`. A null, omitted, or blank slug MUST be regenerated from the submitted name by removing Vietnamese diacritics, mapping `đ`/`Đ` to `d`, lowercasing, and collapsing whitespace into single hyphens. A generated value that cannot satisfy the format MUST produce HTTP 400.

#### Scenario: Normalize explicit slug
- **WHEN** a valid update supplies slug `  LAPTOP-GAMING  `
- **THEN** the persisted and returned slug is `laptop-gaming`

#### Scenario: Regenerate Vietnamese slug
- **WHEN** an update changes the name to `Điện thoại Đẹp` and omits the slug or supplies null, an empty string, or whitespace
- **THEN** the persisted and returned slug is `dien-thoai-dep`, replacing any previous slug

#### Scenario: Invalid explicit or generated slug
- **WHEN** an update supplies an explicit slug containing underscores, punctuation, leading/trailing hyphens, or repeated hyphens, or the submitted name generates no valid slug characters
- **THEN** HTTP 400 is returned with a `slug` error and no update is persisted

### Requirement: Category Update Slug Uniqueness
The updated slug SHALL be unique across every other category, including inactive categories, while the target category MUST be excluded from its own uniqueness check. A duplicate detected before saving or by a database unique constraint during saving MUST return HTTP 409 ProblemDetails without exposing database exception messages or SQL.

#### Scenario: Another category owns the slug
- **WHEN** the normalized requested or generated slug belongs to another active or inactive category
- **THEN** HTTP 409 is returned and the target remains unchanged

#### Scenario: Keep current slug
- **WHEN** the updated slug equals the target category's current slug and no other category owns it
- **THEN** the update succeeds

#### Scenario: Unique conflict occurs after precheck
- **GIVEN** the uniqueness precheck passes
- **WHEN** a concurrent write claims the same slug before this update is saved
- **THEN** HTTP 409 with a safe conflict message is returned and the update is not partially persisted

### Requirement: Category Update Parent Validation
The system SHALL accept a null parent to make the category a root and SHALL require any non-null parent to exist. It MUST reject choosing the category itself or any descendant, regardless of active status or depth, with HTTP 400 and a `parentId` error. Rejected updates MUST leave stored category fields and timestamps unchanged.

#### Scenario: Self-parenting
- **WHEN** an update sets `ParentId` equal to the target ID
- **THEN** HTTP 400 with a `parentId` error is returned

#### Scenario: Direct or indirect descendant parent
- **GIVEN** category A has child B and B has child C, with any combination of active states
- **WHEN** A is updated to use B or C as its parent
- **THEN** HTTP 400 with a `parentId` error is returned

#### Scenario: Missing parent
- **WHEN** an update references a parent ID absent from the database
- **THEN** HTTP 400 with a `parentId` error is returned, distinguishing it from a missing target

#### Scenario: Existing unrelated parent
- **WHEN** the new parent exists outside the target's subtree, including an inactive parent
- **THEN** parent validation succeeds

#### Scenario: Keep current valid parent
- **WHEN** an otherwise valid update retains its existing parent
- **THEN** parent validation succeeds

### Requirement: Concurrent Category Hierarchy Integrity
The system MUST prevent concurrent category updates from jointly introducing a parent cycle even when each proposed relationship would be valid against the original tree. Validation and persistence of parent changes SHALL observe a consistent ordering of hierarchy updates.

#### Scenario: Concurrent reciprocal reparenting
- **GIVEN** A and B initially have no parent
- **WHEN** concurrent requests attempt to set A's parent to B and B's parent to A
- **THEN** at most one succeeds and the other is rejected with a `parentId` validation error after observing the committed hierarchy
- **AND** no cycle is persisted

### Requirement: Category Update Authorization
The update endpoint SHALL validate JWT Bearer tokens and restrict category updates to the Admin role specified for UC-CAT-03. Authentication and authorization failures MUST occur before category mutation.

#### Scenario: Unauthenticated update
- **WHEN** the caller supplies no token or an invalid or expired token
- **THEN** the API returns HTTP 401 and makes no category changes

#### Scenario: Non-admin update
- **WHEN** an authenticated Manager, SalesStaff, or WarehouseStaff attempts an update
- **THEN** the API returns HTTP 403 and makes no category changes

#### Scenario: Admin update
- **WHEN** a caller with a valid Admin JWT submits a valid category update
- **THEN** authorization permits the request to execute

### Requirement: Category Update Request Cancellation
The system SHALL honor request cancellation during category update processing, stop pending database work, and roll back any uncommitted update. Cancellation MUST NOT be reported as a successful update or converted into a business validation or slug conflict result.

#### Scenario: Cancellation before persistence completes
- **WHEN** the caller cancels while the update is waiting for its hierarchy lock or validating or saving data before commit
- **THEN** cancellation propagates, any open transaction is cleaned up, and no uncommitted changes are persisted
