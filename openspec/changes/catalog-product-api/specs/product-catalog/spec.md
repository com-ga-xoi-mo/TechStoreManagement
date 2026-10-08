# Spec Delta

## Purpose

Lets staff and client applications find products, inspect a product with its hardware variants and effective specifications, and register new products with variants, through the TechStore REST API with consistent validation and paginated results.

## ADDED Requirements

### Requirement: Paginated product listing
`GET /api/products` SHALL return a paginated result containing `items`, `page`, `pageSize`, `totalCount` and `totalPages`. `page` defaults to 1 and values below 1 are treated as 1. `pageSize` defaults to 20 and is clamped to the range 1..100. `totalCount` SHALL count all products matching the same filters as `items`.

#### Scenario: Default paging
- **WHEN** a client calls `GET /api/products` without paging parameters
- **THEN** the response is 200 with `page` = 1, `pageSize` = 20, at most 20 items, and `totalCount` equal to the number of matching products

#### Scenario: Out-of-range paging values are normalized
- **WHEN** a client calls `GET /api/products?page=0&pageSize=500`
- **THEN** the response uses `page` = 1 and `pageSize` = 100

#### Scenario: Page beyond the last page
- **WHEN** a client requests a page number greater than `totalPages`
- **THEN** the response is 200 with an empty `items` array and the correct `totalCount` and `totalPages`

#### Scenario: Stable ordering across pages
- **WHEN** a client walks through every page of a result set with an unchanged dataset and the same sort
- **THEN** every matching product appears exactly once across all pages

### Requirement: Keyword search
The `q` parameter SHALL match, case-insensitively and as a substring, the product name, product SKU, product barcode, and the SKU, barcode or variant name of any of the product's variants. Leading and trailing whitespace is ignored and an empty `q` applies no keyword filter.

#### Scenario: Search by product name
- **WHEN** a client calls `GET /api/products?q=iphone`
- **THEN** products whose name contains "iPhone" in any letter case are returned

#### Scenario: Search by variant SKU
- **WHEN** a client calls `GET /api/products?q=IP15PM-256-NT`
- **THEN** the product owning the variant with SKU `IP15PM-256-NT` is returned once

### Requirement: Attribute filters
The listing SHALL support filtering by `brand` (case-insensitive exact match), `categoryId` (products directly assigned to that category only, excluding subcategories) and `isActive`. When `isActive` is omitted, both active and inactive products are returned. Filters combine with AND.

#### Scenario: Filter by brand and category
- **WHEN** a client calls `GET /api/products?brand=apple&categoryId={laptopsId}`
- **THEN** only Apple products assigned directly to the Laptops category are returned

#### Scenario: Subcategory products are not included
- **WHEN** a product is assigned to a child category of `{parentId}` and a client filters by `categoryId={parentId}`
- **THEN** that product is not returned

### Requirement: Display price and price range filter
Each product SHALL expose a display price range: when the product has variants, `priceFrom` and `priceTo` are the minimum and maximum variant prices; otherwise both equal the product base price. `minPrice` and `maxPrice` SHALL select products whose display price range overlaps the requested range.

#### Scenario: Product with variants
- **WHEN** a product has variants priced 20,000,000 and 25,000,000
- **THEN** its summary shows `priceFrom` = 20000000 and `priceTo` = 25000000

#### Scenario: Range overlap
- **WHEN** a client calls `GET /api/products?minPrice=22000000&maxPrice=30000000`
- **THEN** a product whose display range is 20,000,000–25,000,000 is returned

#### Scenario: Inverted range is rejected
- **WHEN** a client sends `minPrice` greater than `maxPrice`
- **THEN** the response is 400 ProblemDetails with an error for `minPrice`

### Requirement: Typed specification filter
The repeatable `spec` parameter (`key:value`, split at the first colon) SHALL filter by containment against effective specs (`product.specs || variant.specs`). Values that parse as numbers match JSON numbers, `true`/`false` match JSON booleans, and other values match JSON strings. All `spec` filters SHALL be satisfied together by the same effective specs.

#### Scenario: Numeric spec value matches JSON number
- **WHEN** a product has specs `{"ram_gb": 16}` and a client calls `GET /api/products?spec=ram_gb:16`
- **THEN** the product is returned

#### Scenario: Product-level spec matched through a variant
- **WHEN** a product has specs `{"cpu": "Apple M3"}`, its variant has specs `{"color": "Space Gray"}`, and a client calls `GET /api/products?spec=cpu:Apple M3&spec=color:Space Gray`
- **THEN** the product is returned because the variant's effective specs contain both values

#### Scenario: Variant overrides product value
- **WHEN** a product has specs `{"color": "Black"}`, its only variant has specs `{"color": "White"}`, and a client calls `GET /api/products?spec=color:Black`
- **THEN** the product is not returned

#### Scenario: Malformed spec parameter
- **WHEN** a client sends `spec=ram_gb` (no colon), `spec=:16` (empty key), `spec=RAM:16` (key not snake_case), `spec=ram_gb:` (empty value), or the same key twice with different values
- **THEN** the response is 400 ProblemDetails with an error for `spec`

### Requirement: Listing sort order
The `sort` parameter SHALL accept `name`, `price` (by `priceFrom`) or `createdAt`, each optionally suffixed with `:asc` or `:desc`. The default is `createdAt:desc`. Products with equal sort values SHALL be ordered deterministically so paging is stable. An unknown sort value SHALL be rejected with 400.

#### Scenario: Sort by price ascending
- **WHEN** a client calls `GET /api/products?sort=price:asc`
- **THEN** items are ordered by `priceFrom` from lowest to highest

#### Scenario: Unknown sort field
- **WHEN** a client calls `GET /api/products?sort=stock`
- **THEN** the response is 400 ProblemDetails with an error for `sort`

### Requirement: Product summary content
Each listing item SHALL contain `id`, `name`, `sku`, `brand`, `categoryId`, `categoryName`, `priceFrom`, `priceTo`, `variantCount`, `isSerialTracked`, `isActive` and `imageUrl`. Listing items SHALL NOT contain stock quantities.

#### Scenario: Summary fields
- **WHEN** a client lists products
- **THEN** each item contains exactly the summary fields and no stock quantity field

### Requirement: Product detail
`GET /api/products/{id}` SHALL return the product's full data (including `barcode`, `costPrice`, `description`, `imageUrl`, `specs`, `isSerialTracked`, `isActive`, category id and name) and its variants, each with `id`, `variantName`, `sku`, `barcode`, `price`, `specs`, `effectiveSpecs` and `isActive`. An unknown id SHALL return 404.

#### Scenario: Detail with effective specs
- **WHEN** a product has specs `{"cpu": "Apple M3", "color": "Silver"}` and a variant with specs `{"color": "Space Gray"}`
- **THEN** that variant's `effectiveSpecs` is `{"cpu": "Apple M3", "color": "Space Gray"}`

#### Scenario: Unknown product
- **WHEN** a client calls `GET /api/products/{id}` with an id that does not exist
- **THEN** the response is 404 ProblemDetails

### Requirement: Specification values keep their JSON types
Every API response and request SHALL represent `specs` and `effectiveSpecs` as JSON objects whose values keep their original JSON type (number, string or boolean).

#### Scenario: Numeric spec stays numeric
- **WHEN** a product is stored with specs `{"ram_gb": 16, "cpu": "Apple M3"}` and fetched
- **THEN** the response contains `"ram_gb": 16` as a JSON number and `"cpu": "Apple M3"` as a JSON string

### Requirement: Product creation
`POST /api/products` SHALL create a product with `categoryId`, `name`, `sku`, `brand`, `basePrice`, `costPrice` and optional `barcode`, `description`, `imageUrl`, `specs` (default `{}`), `isSerialTracked` (default true) and `variants`, atomically. Success SHALL return 201 with a `Location` header to the new product and the product detail as body.

#### Scenario: Create product with variants
- **WHEN** a client posts a valid product with two valid variants
- **THEN** the response is 201, `Location` points to `/api/products/{newId}`, and the body contains the product with both variants

#### Scenario: Atomic creation
- **WHEN** a create request fails for any reason
- **THEN** neither the product nor any of its variants is stored

### Requirement: Required fields and length limits
Product creation SHALL reject requests where `categoryId`, `basePrice`, `costPrice` or a variant's `price` is missing, or where `name`, `sku`, `brand`, a variant's `variantName` or `sku` is missing, empty or whitespace. Lengths SHALL not exceed: `name`/`variantName` 200, `brand` 100, `sku`/`barcode` 64, `description` 2000, `imageUrl` 2048 characters.

#### Scenario: Empty name
- **WHEN** a client posts a product whose `name` is whitespace only
- **THEN** the response is 400 with an error for `name`

#### Scenario: Missing required fields
- **WHEN** a client posts a product without `basePrice` and without `categoryId`
- **THEN** a single 400 response contains errors for both `basePrice` and `categoryId`, and no product is created with a default price of 0

#### Scenario: Overlong SKU
- **WHEN** a client posts a product whose `sku` has 65 characters
- **THEN** the response is 400 with an error for `sku`

### Requirement: Non-negative prices
Product creation SHALL reject a negative `basePrice`, `costPrice` or variant `price`.

#### Scenario: Negative base price
- **WHEN** a client posts a product with `basePrice` = -50000
- **THEN** the response is 400 with an error for `basePrice` and nothing is stored

#### Scenario: Negative variant price
- **WHEN** a client posts a product whose second variant has `price` = -1
- **THEN** the response is 400 with an error for `variants[1].price`

### Requirement: Category must exist
Product creation SHALL reject a `categoryId` that does not reference an existing category.

#### Scenario: Unknown category
- **WHEN** a client posts a product with a random `categoryId`
- **THEN** the response is 400 with an error for `categoryId`

### Requirement: SKU uniqueness
SKUs SHALL be unique, ignoring letter case, across all products and all variants. Duplicates inside one request SHALL be rejected with 400. A SKU already used by an existing product or variant SHALL be rejected with 409, including when two concurrent requests race to create the same SKU.

#### Scenario: Duplicate inside the request
- **WHEN** a client posts a product whose variant SKU equals the product SKU
- **THEN** the response is 400 with an error for `variants[0].sku`

#### Scenario: SKU already used by another product's variant
- **WHEN** a client posts a product with `sku` = `ip15pm-256-nt` while a variant `IP15PM-256-NT` exists
- **THEN** the response is 409 ProblemDetails identifying the conflicting SKU

#### Scenario: Concurrent creation with the same SKU
- **WHEN** two create requests with the same new SKU are processed at the same time
- **THEN** exactly one returns 201 and the other returns 409

### Requirement: Specification format validation
Product and variant `specs` SHALL be flat JSON objects whose keys match `^[a-z][a-z0-9_]*$` and whose values are strings, numbers or booleans. Nested objects, arrays and null values SHALL be rejected.

#### Scenario: Invalid key
- **WHEN** a client posts a product with specs `{"RAM GB": 16}`
- **THEN** the response is 400 with an error for `specs`

#### Scenario: Nested value
- **WHEN** a client posts a variant with specs `{"display": {"size_inch": 14}}`
- **THEN** the response is 400 with an error for `variants[0].specs`

### Requirement: Field-level validation errors
Validation failures SHALL return 400 as RFC 7807 ProblemDetails with an `errors` object keyed by camelCase field path (for example `basePrice`, `variants[1].sku`), listing all detected input errors in one response.

#### Scenario: Multiple errors reported together
- **WHEN** a client posts a product with an empty `name` and a negative `costPrice`
- **THEN** a single 400 response contains errors for both `name` and `costPrice`

### Requirement: Creation does not create stock
Creating a product SHALL NOT create any inventory stock, movement or serial record.

#### Scenario: No stock row after creation
- **WHEN** a product is created successfully
- **THEN** no `inventory_stocks`, `inventory_movements` or `serial_imeis` row references the new product
