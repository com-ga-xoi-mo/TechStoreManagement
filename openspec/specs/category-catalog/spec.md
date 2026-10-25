# category-catalog Specification

## Purpose

Provides hierarchical category navigation trees and administrative category management for the product catalog.

## Requirements

### Requirement: Authenticated Category Tree Retrieval
The system SHALL return every category, including hidden (`IsActive = false`) ones, organized into a hierarchical tree where root categories contain nested child categories via direct route `/api/categories/tree`. The request MUST carry a valid Bearer token; every authenticated role (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) MAY read the tree, and an unauthenticated request MUST be rejected.

#### Scenario: Retrieve complete category tree
- **WHEN** an authenticated client sends a GET request to `/api/categories/tree`
- **THEN** system returns HTTP 200 with all root categories and their recursively nested subcategories, ordered by name at every level

#### Scenario: Hidden categories remain visible to the management workspace
- **WHEN** a category has `IsActive` set to false
- **THEN** system still returns it in the tree so the management workspace can display its "Đã ẩn" state and offer reactivation

#### Scenario: Branch totals include descendants
- **WHEN** a root or parent category has products assigned directly and through its descendants
- **THEN** the returned `ProductCount` for that branch includes its own products plus every descendant branch, and `SkuCount` reports the category's own SKU count

#### Scenario: Unauthenticated tree request
- **WHEN** a client requests `/api/categories/tree` without a token, or with an invalid or expired token
- **THEN** system rejects the request with HTTP 401 Unauthorized and returns no category data

### Requirement: Category Creation
The system SHALL allow users with the Admin role to create a new category with a validated name, unique slug, and optional valid parent category via direct route `/api/categories`.

#### Scenario: Create top-level category successfully
- **WHEN** an Admin posts valid category details without a parent ID to `/api/categories`
- **THEN** system creates the category and returns HTTP 201 Created with the created Category DTO and a Location header targeting `/api/categories/{id}`

#### Scenario: Create subcategory with valid parent ID
- **WHEN** an Admin posts valid category details referencing an existing parent ID to `/api/categories`
- **THEN** system validates parent existence, links the subcategory, and returns HTTP 201 Created with a Location header targeting `/api/categories/{id}`

#### Scenario: Category creation with duplicate slug
- **WHEN** client posts a category with a slug that already exists in the system
- **THEN** system rejects the request and returns HTTP 409 Conflict formatted as an RFC 7807 ProblemDetails payload

#### Scenario: Category creation with invalid parent ID
- **WHEN** client posts a category with a `ParentId` that does not exist in the database
- **THEN** system rejects the request and returns HTTP 400 Bad Request indicating the parent category was not found

#### Scenario: Category creation with invalid input payload
- **WHEN** client posts category data missing a required Name or containing an invalid Slug format
- **THEN** system rejects the request and returns HTTP 400 Bad Request with field-level validation errors

#### Scenario: Category creation unauthorized access
- **WHEN** an unauthenticated request or a user with a role other than Admin attempts to create a category
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden
