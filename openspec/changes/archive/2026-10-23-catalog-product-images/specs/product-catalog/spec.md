# Spec Delta

## MODIFIED Requirements

### Requirement: Product creation
`POST /api/products` SHALL create a product with `categoryId`, `name`, `sku`, `brand`, `basePrice`, `costPrice` and optional `barcode`, `description`, `specs` (default `{}`), `isSerialTracked` (default true) and `variants`, atomically. The request has no `imageUrl`; a new product has no images and `imageUrl` null until images are uploaded. The new product SHALL be stored not selling (`isActive` false) and each variant SHALL be stored not selling unless its item has `isActive` true; the owner opens them for sale later. Success SHALL return 201 with a `Location` header to the new product and the product detail as body.

#### Scenario: Create product with variants
- **WHEN** a client posts a valid product with two valid variants
- **THEN** the response is 201, `Location` points to `/api/products/{newId}`, and the body contains the product with both variants

#### Scenario: New product and variants start not selling
- **WHEN** a client posts a valid product with one variant without `isActive`
- **THEN** the created product has `isActive` false, its variant has `isActive` false, and `GET /api/products?isActive=true` does not return it

#### Scenario: Atomic creation
- **WHEN** a create request fails for any reason
- **THEN** neither the product nor any of its variants is stored

#### Scenario: Image URL in the request is ignored
- **WHEN** an older client posts a valid product with `"imageUrl": "https://example.com/a.png"`
- **THEN** the product is created with `imageUrl` null and an empty `images` list

### Requirement: Product update
`PUT /api/products/{id}` SHALL replace the product's editable fields `categoryId`, `name`, `brand`, `barcode`, `basePrice`, `costPrice`, `description`, `specs` and `isSerialTracked`, and apply the request's variant items, atomically. Omitted optional fields SHALL be stored as empty (`barcode`, `description` become null, `specs` becomes `{}`). The update SHALL NOT change the product's images or its `imageUrl`. Success SHALL return 200 with the product detail and a refreshed `updatedAt`. An unknown id SHALL return 404.

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

#### Scenario: Images survive an update
- **WHEN** a product has two images and an Admin updates its name
- **THEN** the response still lists both images and `imageUrl` is unchanged

### Requirement: Required fields and length limits
Product creation SHALL reject requests where `categoryId`, `basePrice`, `costPrice` or a variant's `price` is missing, or where `name`, `sku`, `brand`, a variant's `variantName` or `sku` is missing, empty or whitespace. Lengths SHALL not exceed: `name`/`variantName` 200, `brand` 100, `sku`/`barcode` 64, `description` 2000 characters.

#### Scenario: Empty name
- **WHEN** a client posts a product whose `name` is whitespace only
- **THEN** the response is 400 with an error for `name`

#### Scenario: Missing required fields
- **WHEN** a client posts a product without `basePrice` and without `categoryId`
- **THEN** a single 400 response contains errors for both `basePrice` and `categoryId`, and no product is created with a default price of 0

#### Scenario: Overlong SKU
- **WHEN** a client posts a product whose `sku` has 65 characters
- **THEN** the response is 400 with an error for `sku`

### Requirement: Product summary content
Each listing item SHALL contain `id`, `name`, `sku`, `brand`, `categoryId`, `categoryName`, `priceFrom`, `priceTo`, `variantCount`, `isSerialTracked`, `isActive` and `imageUrl`, where `imageUrl` is the product's cover image computed by the server from its images. Listing items SHALL NOT contain stock quantities.

#### Scenario: Summary fields
- **WHEN** a client lists products
- **THEN** each item contains exactly the summary fields and no stock quantity field

#### Scenario: Cover image in the listing
- **WHEN** a product's first shared image has URL `U`
- **THEN** its listing item has `imageUrl` `U`

### Requirement: Product detail
`GET /api/products/{id}` SHALL return the product's full data (including `barcode`, `costPrice`, `description`, `imageUrl`, `images`, `specs`, `isSerialTracked`, `isActive`, category id and name) and its variants, each with `id`, `variantName`, `sku`, `barcode`, `price`, `specs`, `effectiveSpecs` and `isActive`. `images` SHALL list every image of the product in sort order, each with `id`, `variantId`, `url`, `contentType`, `sizeBytes` and `sortOrder`. An unknown id SHALL return 404.

#### Scenario: Detail with effective specs
- **WHEN** a product has specs `{"cpu": "Apple M3", "color": "Silver"}` and a variant with specs `{"color": "Space Gray"}`
- **THEN** that variant's `effectiveSpecs` is `{"cpu": "Apple M3", "color": "Space Gray"}`

#### Scenario: Unknown product
- **WHEN** a client calls `GET /api/products/{id}` with an id that does not exist
- **THEN** the response is 404 ProblemDetails

#### Scenario: Images in order
- **WHEN** a product has a shared image and a "Đen" image at sort orders 0 and 1
- **THEN** `images` lists them in that order with `variantId` null and the "Đen" variant id

### Requirement: Delete only unused products
`DELETE /api/products/{id}` SHALL permanently remove a product, all its variants and all its images when the product is not in use, returning 204. The stored image files SHALL be deleted after the database change is committed; a failure to delete them SHALL NOT fail the request. A product in use SHALL NOT be deleted and SHALL return 409 ProblemDetails telling the caller to deactivate it instead. An unknown id SHALL return 404. The check and the removal SHALL be atomic with respect to concurrent stock or order writes.

#### Scenario: Delete a new product
- **WHEN** an Admin deletes a product that has never been stocked, purchased or sold
- **THEN** the response is 204 and `GET /api/products/{id}` returns 404

#### Scenario: Delete a product with history
- **WHEN** an Admin deletes a product that appears in an order line
- **THEN** the response is 409 ProblemDetails, the product still exists, and the message suggests deactivating it

#### Scenario: Unknown product
- **WHEN** an Admin deletes an id that does not exist
- **THEN** the response is 404 ProblemDetails

#### Scenario: Images removed with the product
- **WHEN** an Admin deletes an unused product that has three images
- **THEN** the response is 204, the three image rows are gone and their files are deleted from storage
