## MODIFIED Requirements

### Requirement: Category must exist
Product creation SHALL reject a `categoryId` that does not reference an existing category, or that references a hidden category (`isActive` false), with the same 400 error for `categoryId` that product update returns.

#### Scenario: Unknown category
- **WHEN** a client posts a product with a random `categoryId`
- **THEN** the response is 400 with an error for `categoryId`

#### Scenario: Hidden category
- **WHEN** a client posts an otherwise valid product whose `categoryId` references an existing category with `isActive` false
- **THEN** the response is 400 with an error for `categoryId`, and no product, variant or spec definition is stored

### Requirement: Update input validation
Product update SHALL apply the same required-field, length, non-negative price and specification format rules as product creation, reporting every detected error in one 400 ProblemDetails keyed by camelCase field path. `categoryId` SHALL reference an existing category. It SHALL be active unless it equals the product's current category, so a product whose category was hidden can still be updated in place; otherwise the response is 400 with an error for `categoryId`.

#### Scenario: Several errors at once
- **WHEN** an update has an empty `name`, a negative `costPrice` and a variant with specs `{"RAM": 8}`
- **THEN** one 400 response contains errors for `name`, `costPrice` and `variants[0].specs`

#### Scenario: Inactive category
- **WHEN** an update moves a product to a category whose `isActive` is false
- **THEN** the response is 400 with an error for `categoryId` and nothing is changed

#### Scenario: Product stays in its hidden category
- **WHEN** a product's current category has been hidden and an otherwise valid update keeps the same `categoryId` while changing the price
- **THEN** the update succeeds with 200 and the product keeps that category

#### Scenario: Unknown category on update
- **WHEN** an update sends a `categoryId` that does not exist
- **THEN** the response is 400 with an error for `categoryId` and nothing is changed
