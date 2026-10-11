# Spec Delta

## MODIFIED Requirements

### Requirement: Category Hierarchy Tree Retrieval
The system SHALL return all active categories organized into a hierarchical tree where root categories contain nested child categories via direct route `/api/categories/tree`.

#### Scenario: Retrieve complete active category tree
- **WHEN** client sends a GET request to `/api/categories/tree`
- **THEN** system returns HTTP 200 with root categories and their recursively nested active subcategories ordered by display hierarchy

#### Scenario: Inactive categories are excluded from tree
- **WHEN** categories have `IsActive` set to false
- **THEN** system omits them and their descendant branches from the tree returned by `/api/categories/tree`

### Requirement: Category Creation
The system SHALL allow authorized users with Admin or Manager roles to create a new category with a validated name, unique slug, and optional valid parent category via direct route `/api/categories`.

#### Scenario: Create top-level category successfully
- **WHEN** an authorized Admin or Manager posts valid category details without a parent ID to `/api/categories`
- **THEN** system creates the category and returns HTTP 201 Created with the created Category DTO and a Location header targeting `/api/categories/{id}`

#### Scenario: Create subcategory with valid parent ID
- **WHEN** an authorized Admin or Manager posts valid category details referencing an existing parent ID to `/api/categories`
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
- **WHEN** an unauthenticated request or a user with a role other than Admin or Manager attempts to create a category
- **THEN** system rejects the request with HTTP 401 Unauthorized or HTTP 403 Forbidden
