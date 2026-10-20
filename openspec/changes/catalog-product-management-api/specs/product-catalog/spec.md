# Spec Delta

## ADDED Requirements

### Requirement: Product update
`PUT /api/products/{id}` SHALL replace the product's editable fields `categoryId`, `name`, `brand`, `barcode`, `basePrice`, `costPrice`, `description`, `imageUrl`, `specs` and `isSerialTracked`, and apply the request's variant items, atomically. Omitted optional fields SHALL be stored as empty (`barcode`, `description`, `imageUrl` become null, `specs` becomes `{}`). Success SHALL return 200 with the product detail and a refreshed `updatedAt`. An unknown id SHALL return 404.

#### Scenario: Update price and specs
- **WHEN** an Admin sends `PUT /api/products/{id}` with a new `basePrice` and specs `{"ram_gb": 16}`
- **THEN** the response is 200, the body shows the new price and specs, and `updatedAt` is set

#### Scenario: Specs are replaced, not merged
- **WHEN** a product has specs `{"cpu": "Apple M3", "ram_gb": 8}` and the update sends specs `{"ram_gb": 16}`
- **THEN** the stored specs are exactly `{"ram_gb": 16}`

#### Scenario: Unknown product
- **WHEN** an Admin sends `PUT /api/products/{id}` with an id that does not exist
- **THEN** the response is 404 ProblemDetails

#### Scenario: Atomic update
- **WHEN** an update request fails validation or a conflict check
- **THEN** neither the product nor any of its variants is changed

#### Scenario: Existing orders keep their prices
- **WHEN** a product that already appears in an order is updated with a new name and price
- **THEN** the existing order lines keep the product name, SKU and unit price they were created with

#### Scenario: Inactive product can be edited
- **WHEN** an Admin updates a product whose `isActive` is false
- **THEN** the update succeeds and the product stays inactive

### Requirement: Update input validation
Product update SHALL apply the same required-field, length, non-negative price and specification format rules as product creation, reporting every detected error in one 400 ProblemDetails keyed by camelCase field path. `categoryId` SHALL reference an existing, active category, otherwise the response is 400 with an error for `categoryId`.

#### Scenario: Several errors at once
- **WHEN** an update has an empty `name`, a negative `costPrice` and a variant with specs `{"RAM": 8}`
- **THEN** one 400 response contains errors for `name`, `costPrice` and `variants[0].specs`

#### Scenario: Inactive category
- **WHEN** an update moves a product to a category whose `isActive` is false
- **THEN** the response is 400 with an error for `categoryId` and nothing is changed

### Requirement: SKUs are immutable after creation
The product SKU SHALL NOT be changeable: the update request has no product `sku` field. For a variant item that references an existing variant, `sku` SHALL be omitted or equal to the stored SKU ignoring letter case; any other value SHALL be rejected with 400 for `variants[i].sku`.

#### Scenario: Changing an existing variant SKU
- **WHEN** an update sends variant `{ "id": <existing>, "sku": "NEW-SKU" }` while the stored SKU is `OLD-SKU`
- **THEN** the response is 400 with an error for `variants[0].sku`

#### Scenario: Same SKU in another letter case
- **WHEN** an update sends variant `{ "id": <existing>, "sku": "old-sku" }` while the stored SKU is `OLD-SKU`
- **THEN** the variant is updated and its SKU stays `OLD-SKU`

### Requirement: Variant upsert on update
Each variant item with an `id` SHALL update that variant's `variantName`, `barcode`, `price`, `specs` and, when given, `isActive`. Each item without `id` SHALL add a new active variant (unless `isActive` is false) whose `sku` is required and unique as on creation. Variants not listed SHALL stay unchanged; no variant is ever deleted by an update.

#### Scenario: Add a new variant
- **WHEN** an update contains a variant item without `id`, with `sku` = `MBA-M3-16-512` and a valid price
- **THEN** the product detail lists the new variant with `isActive` = true

#### Scenario: Retire a variant
- **WHEN** an update sends `{ "id": <existing>, "isActive": false, ... }`
- **THEN** that variant becomes inactive and the other variants are unchanged

#### Scenario: Unlisted variants are kept
- **WHEN** a product has three variants and the update lists only one of them
- **THEN** the product still has three variants after the update

#### Scenario: Variant id from another product
- **WHEN** an update sends a variant `id` that does not belong to the product
- **THEN** the response is 400 with an error for `variants[i].id`

#### Scenario: Same variant listed twice
- **WHEN** an update lists the same variant `id` in two items
- **THEN** the response is 400 with an error for the second item's `id`

#### Scenario: New variant SKU already used
- **WHEN** a new variant item uses a SKU that already belongs to any product or variant, ignoring letter case
- **THEN** the response is 409 ProblemDetails identifying the conflicting SKU

#### Scenario: New variant SKU duplicated in the request
- **WHEN** two new variant items share a SKU, or a new item reuses the product SKU
- **THEN** the response is 400 with an error for the later item's `sku`

### Requirement: Changes blocked once a product is in use
A product is in use when any inventory stock, serial/IMEI, purchase order line or order line references it. For a product in use, changing `isSerialTracked` SHALL be rejected with 409, and adding the first variant to a product that has no variants SHALL be rejected with 409, leaving the product unchanged.

#### Scenario: Serial tracking change on a stocked product
- **WHEN** a product with serial/IMEI records is updated with a different `isSerialTracked`
- **THEN** the response is 409 ProblemDetails and the flag is unchanged

#### Scenario: Serial tracking change on an unused product
- **WHEN** a product that is not in use is updated with a different `isSerialTracked`
- **THEN** the update succeeds

#### Scenario: First variant on a product with product-level stock
- **WHEN** a product without variants has an inventory stock row and the update adds a variant
- **THEN** the response is 409 ProblemDetails and no variant is added

#### Scenario: Another variant on a product that already has variants
- **WHEN** a product that has variants and stock is updated with one more new variant
- **THEN** the update succeeds

### Requirement: No product barcode when variants exist
A product that has at least one variant, counting variants added by the same request, SHALL NOT carry a product-level `barcode`. Product creation and update SHALL reject a non-empty `barcode` in that case with 400 for `barcode`. Variant barcodes are unaffected.

#### Scenario: Create with variants and a product barcode
- **WHEN** a client posts a product with `barcode` = `8934567890123` and one variant
- **THEN** the response is 400 with an error for `barcode`

#### Scenario: Update adds a variant while a barcode is kept
- **WHEN** a product without variants has a barcode and an update keeps that barcode and adds a variant
- **THEN** the response is 400 with an error for `barcode`

#### Scenario: Product without variants keeps its barcode
- **WHEN** a client posts a product with a barcode and no variants
- **THEN** the product is created with that barcode

### Requirement: Product activation status
`PATCH /api/products/{id}/status` with body `{ "isActive": true|false }` SHALL set the product's `isActive` and return 200 with the product detail. A missing `isActive` SHALL return 400. Setting the value the product already has SHALL succeed without changing `updatedAt`. Variant `isActive` flags SHALL NOT be changed. An unknown id SHALL return 404.

#### Scenario: Stop selling a product
- **WHEN** an Admin sends `PATCH /api/products/{id}/status` with `{ "isActive": false }`
- **THEN** the response is 200 with `isActive` = false, and `GET /api/products?isActive=true` no longer returns the product

#### Scenario: Resume selling
- **WHEN** an Admin sends `{ "isActive": true }` for an inactive product
- **THEN** the product is active again and its variants keep their own flags

#### Scenario: Repeated deactivation
- **WHEN** the product is already inactive and the same request is sent again
- **THEN** the response is 200 and `updatedAt` is unchanged

#### Scenario: Missing flag
- **WHEN** the request body is `{}`
- **THEN** the response is 400 with an error for `isActive`

### Requirement: Delete only unused products
`DELETE /api/products/{id}` SHALL permanently remove a product and all its variants when the product is not in use, returning 204. A product in use SHALL NOT be deleted and SHALL return 409 ProblemDetails telling the caller to deactivate it instead. An unknown id SHALL return 404. The check and the removal SHALL be atomic with respect to concurrent stock or order writes.

#### Scenario: Delete a new product
- **WHEN** an Admin deletes a product that has never been stocked, purchased or sold
- **THEN** the response is 204 and `GET /api/products/{id}` returns 404

#### Scenario: Delete a product with history
- **WHEN** an Admin deletes a product that appears in an order line
- **THEN** the response is 409 ProblemDetails, the product still exists, and the message suggests deactivating it

#### Scenario: Unknown product
- **WHEN** an Admin deletes an id that does not exist
- **THEN** the response is 404 ProblemDetails

### Requirement: Product write authorization
`POST /api/products`, `PUT /api/products/{id}`, `PATCH /api/products/{id}/status` and `DELETE /api/products/{id}` SHALL require a valid bearer token whose role is `Admin` or `Manager`. A request without a valid token SHALL return 401 and a request from another role SHALL return 403, in both cases without changing data. Product read endpoints are not changed by this requirement.

#### Scenario: Missing token
- **WHEN** a client calls `PUT /api/products/{id}` without a bearer token
- **THEN** the response is 401 and the product is unchanged

#### Scenario: Sales staff cannot delete
- **WHEN** a user with role `SalesStaff` calls `DELETE /api/products/{id}`
- **THEN** the response is 403 and the product still exists

#### Scenario: Manager can update
- **WHEN** a user with role `Manager` sends a valid update
- **THEN** the response is 200

#### Scenario: Reads stay open
- **WHEN** a client calls `GET /api/products` without a token
- **THEN** the response is 200 as before
