# Spec Delta

## MODIFIED Requirements

### Requirement: Product detail
`GET /api/products/{id}` SHALL return the product's full data (including `barcode`, `costPrice`, `description`, `imageUrl`, `images`, `specs`, `isSerialTracked`, `isActive`, category id and name) and its variants, each with `id`, `variantName`, `sku`, `barcode`, `price`, `specs`, `effectiveSpecs` and `isActive`. `images` SHALL list every image of the product in sort order, each with `id`, `variantId`, `url`, `contentType`, `sizeBytes` and `sortOrder`. `costPrice` SHALL be filled only when the caller's role is `Admin` or `Manager`; for `SalesStaff` and `WarehouseStaff` it SHALL be `null`. An unknown id SHALL return 404.

#### Scenario: Detail with effective specs
- **WHEN** a product has specs `{"cpu": "Apple M3", "color": "Silver"}` and a variant with specs `{"color": "Space Gray"}`
- **THEN** that variant's `effectiveSpecs` is `{"cpu": "Apple M3", "color": "Space Gray"}`

#### Scenario: Unknown product
- **WHEN** a client calls `GET /api/products/{id}` with an id that does not exist
- **THEN** the response is 404 ProblemDetails

#### Scenario: Images in order
- **WHEN** a product has a shared image and a "Đen" image at sort orders 0 and 1
- **THEN** `images` lists them in that order with `variantId` null and the "Đen" variant id

#### Scenario: Manager sees cost price
- **WHEN** a `Manager` calls `GET /api/products/{id}` for a product with cost 17,000,000
- **THEN** `costPrice` is 17000000

#### Scenario: Sales and warehouse do not receive cost price
- **WHEN** a `SalesStaff` or `WarehouseStaff` calls `GET /api/products/{id}` directly
- **THEN** the response is 200 with `costPrice` null and every other field unchanged
