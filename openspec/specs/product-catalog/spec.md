# product-catalog Specification

## Purpose

Lets staff and client applications find products, inspect a product with its hardware variants and effective specifications, and register new products with variants, through the TechStore REST API with consistent validation and paginated results.

## Requirements

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
The `q` parameter SHALL match, case-insensitively and as a substring, the product name, product SKU, product brand, product barcode, and the SKU, barcode or variant name of any of the product's variants. Leading and trailing whitespace is ignored and an empty `q` applies no keyword filter.

#### Scenario: Search by product name
- **WHEN** a client calls `GET /api/products?q=iphone`
- **THEN** products whose name contains "iPhone" in any letter case are returned

#### Scenario: Search by variant SKU
- **WHEN** a client calls `GET /api/products?q=IP15PM-256-NT`
- **THEN** the product owning the variant with SKU `IP15PM-256-NT` is returned once

#### Scenario: Search by brand
- **WHEN** a client calls `GET /api/products?q=logi`
- **THEN** products whose brand contains "Logi" in any letter case are returned, even when their name does not contain it

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
The repeatable `spec` parameter (`key:value`, split at the first colon) SHALL filter by containment against effective specs (`product.specs || variant.specs`). A value wrapped in double quotes (`"5.2"`) SHALL match the JSON string inside the quotes. Otherwise, values that parse as numbers match JSON numbers, `true`/`false` match JSON booleans, and other values match JSON strings. All `spec` filters SHALL be satisfied together by the same effective specs.

#### Scenario: Numeric spec value matches JSON number
- **WHEN** a product has specs `{"ram_gb": 16}` and a client calls `GET /api/products?spec=ram_gb:16`
- **THEN** the product is returned

#### Scenario: Quoted value matches a JSON string
- **WHEN** a product has specs `{"bluetooth": "5.2"}` and a client calls `GET /api/products?spec=bluetooth:"5.2"`
- **THEN** the product is returned, while `spec=bluetooth:5.2` (a number) does not return it

#### Scenario: Product-level spec matched through a variant
- **WHEN** a product has specs `{"cpu": "Apple M3"}`, its variant has specs `{"color": "Space Gray"}`, and a client calls `GET /api/products?spec=cpu:Apple M3&spec=color:Space Gray`
- **THEN** the product is returned because the variant's effective specs contain both values

#### Scenario: Variant overrides product value
- **WHEN** a product has specs `{"color": "Black"}`, its only variant has specs `{"color": "White"}`, and a client calls `GET /api/products?spec=color:Black`
- **THEN** the product is not returned

#### Scenario: Malformed spec parameter
- **WHEN** a client sends `spec=ram_gb` (no colon), `spec=:16` (empty key), `spec=RAM:16` (key not snake_case), `spec=ram_gb:` (empty value), `spec=ram_gb:""` (empty quoted value), or the same key twice with different values
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

### Requirement: Specification values keep their JSON types
Every API response and request SHALL represent `specs` and `effectiveSpecs` as JSON objects whose values keep their original JSON type (number, string or boolean).

#### Scenario: Numeric spec stays numeric
- **WHEN** a product is stored with specs `{"ram_gb": 16, "cpu": "Apple M3"}` and fetched
- **THEN** the response contains `"ram_gb": 16` as a JSON number and `"cpu": "Apple M3"` as a JSON string

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

### Requirement: Non-negative prices
Product creation SHALL reject a negative `basePrice`, `costPrice` or variant `price`.

#### Scenario: Negative base price
- **WHEN** a client posts a product with `basePrice` = -50000
- **THEN** the response is 400 with an error for `basePrice` and nothing is stored

#### Scenario: Negative variant price
- **WHEN** a client posts a product whose second variant has `price` = -1
- **THEN** the response is 400 with an error for `variants[1].price`

### Requirement: Category must exist
Product creation SHALL reject a `categoryId` that does not reference an existing category, or that references a hidden category (`isActive` false), with the same 400 error for `categoryId` that product update returns.

#### Scenario: Unknown category
- **WHEN** a client posts a product with a random `categoryId`
- **THEN** the response is 400 with an error for `categoryId`

#### Scenario: Hidden category
- **WHEN** a client posts an otherwise valid product whose `categoryId` references an existing category with `isActive` false
- **THEN** the response is 400 with an error for `categoryId`, and no product, variant or spec definition is stored

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

### Requirement: SKUs are immutable after creation
The product SKU SHALL NOT be changeable: the update request has no product `sku` field. For a variant item that references an existing variant, `sku` SHALL be omitted or equal to the stored SKU ignoring letter case; any other value SHALL be rejected with 400 for `variants[i].sku`.

#### Scenario: Changing an existing variant SKU
- **WHEN** an update sends variant `{ "id": <existing>, "sku": "NEW-SKU" }` while the stored SKU is `OLD-SKU`
- **THEN** the response is 400 with an error for `variants[0].sku`

#### Scenario: Same SKU in another letter case
- **WHEN** an update sends variant `{ "id": <existing>, "sku": "old-sku" }` while the stored SKU is `OLD-SKU`
- **THEN** the variant is updated and its SKU stays `OLD-SKU`

### Requirement: Variant upsert on update
Each variant item with an `id` SHALL update that variant's `variantName`, `barcode`, `price`, `specs` and, when given, `isActive`. Each item without `id` SHALL add a new variant that is not selling (`isActive` false) unless the item has `isActive` true, whose `sku` is required and unique as on creation. Variants not listed SHALL stay unchanged; no variant is ever deleted by an update.

#### Scenario: Add a new variant
- **WHEN** an update contains a variant item without `id`, with `sku` = `MBA-M3-16-512`, a valid price and no `isActive`
- **THEN** the product detail lists the new variant with `isActive` = false

#### Scenario: Add a variant that sells at once
- **WHEN** a new variant item has `isActive` true
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

### Requirement: Specification filter suggestions
`GET /api/products/spec-keys` SHALL return, without authentication, each spec key that occurs in the effective specs of stored products (active and inactive), with the number of distinct products having it and its distinct scalar values. Each value SHALL carry a display `label`, its product count and a `filterValue` that, sent as `spec=<key>:<filterValue>`, matches exactly that value. An optional `categoryId` SHALL limit the products to that category only.

#### Scenario: Keys with their values
- **WHEN** products have specs `{"ram_gb": 16}` and `{"ram_gb": 8, "cpu": "Apple M3"}` and a client calls `GET /api/products/spec-keys`
- **THEN** the response is 200 with `ram_gb` (2 products, values 8 then 16) before `cpu` (1 product, value `Apple M3`)

#### Scenario: Ordering and limits
- **WHEN** the response is built
- **THEN** keys are ordered by product count descending then key; numeric values ascending, then strings in ordinal order, then `false` before `true`; at most 100 values per key; object, array and null values are skipped; an unknown `categoryId` gives an empty list

#### Scenario: Variant specs count
- **WHEN** a product has specs `{"cpu": "Apple M3"}` and its variant has specs `{"color": "Space Gray"}`
- **THEN** both `cpu` and `color` are listed for that product

#### Scenario: Category scope
- **WHEN** a client calls `GET /api/products/spec-keys?categoryId=<accessories>`
- **THEN** only keys of products directly in that category are returned

#### Scenario: String that looks like a number
- **WHEN** a product has specs `{"bluetooth": "5.2"}`
- **THEN** the value has `label` `5.2` and `filterValue` `"5.2"` (quoted), and `GET /api/products?spec=bluetooth:"5.2"` returns the product

### Requirement: Specification definitions
The `spec_definitions` table SHALL hold one row per spec key with a display `label` (unique ignoring letter case), an optional `unit` and a `valueKind` (`number`, `string` or `boolean`), seeded with the keys used by the catalog seed data. `GET /api/products/spec-definitions` SHALL return all definitions without authentication, ordered by label, and `GET /api/products/spec-keys` SHALL add `label`, `unit` and `valueKind` to every key that has a definition.

#### Scenario: Seeded definition
- **WHEN** a client calls `GET /api/products/spec-definitions` on a fresh database
- **THEN** the list contains `{ "key": "ram_gb", "label": "RAM", "unit": "GB", "valueKind": "number" }`

#### Scenario: Suggestion carries the label
- **WHEN** a client calls `GET /api/products/spec-keys` and `ram_gb` has a definition
- **THEN** the `ram_gb` entry has `label` `RAM` and `unit` `GB`; a key without a definition has null `label`, `unit` and `valueKind`

### Requirement: New specification names are saved with the product
Product creation and update SHALL accept an optional `specLabels` map from spec key to `{ label, unit, valueKind }`. Every key in it SHALL appear in the request's product or variant specs, have a non-empty label of at most 100 characters, a unit of at most 20 characters and a `valueKind` matching the JSON type of its values, otherwise 400 for `specLabels.<key>`. A definition SHALL be inserted, in the same save as the product, only for a key that has none; an existing definition is never changed.

#### Scenario: New spec with its name
- **WHEN** an Admin creates a product with specs `{"tan_so_quet_hz": 144}` and `specLabels` `{"tan_so_quet_hz": {"label": "Tần số quét", "unit": "Hz", "valueKind": "number"}}`
- **THEN** the product is created and `GET /api/products/spec-definitions` lists `tan_so_quet_hz` as `Tần số quét` (Hz)

#### Scenario: Existing definition is kept
- **WHEN** an update sends `specLabels` `{"ram_gb": {"label": "Bộ nhớ RAM", "unit": "GB", "valueKind": "number"}}`
- **THEN** the update succeeds and the `ram_gb` label stays `RAM`

#### Scenario: Label already used by another key
- **WHEN** `specLabels` gives a new key the label `ram` (another key already has `RAM`)
- **THEN** the response is 400 with an error for `specLabels.<key>` and nothing is saved

#### Scenario: Name for a key that is not in the specs
- **WHEN** `specLabels` names a key that no spec in the request uses
- **THEN** the response is 400 with an error for `specLabels.<key>`

### Requirement: Specification values follow their definition
When a spec key has a definition, product creation and update SHALL reject a value of another JSON type with 400 for `specs` (product specs) or `variants[i].specs`. Keys without a definition keep the existing rules.

#### Scenario: Text for a number spec
- **WHEN** an Admin saves a product with specs `{"ram_gb": "16 GB"}`
- **THEN** the response is 400 with an error for `specs` naming `ram_gb`

### Requirement: Brand spelling is reused
Product creation and update SHALL store the brand with the spelling already used by other products when the trimmed brand equals it ignoring letter case; when several spellings exist the one used by most products wins. The product being updated SHALL not count, so it can change the letter case of a brand only it uses. A brand that no other product uses is stored as typed (trimmed).

#### Scenario: Lower-case brand on create
- **WHEN** products with brand `Apple` exist and an Admin creates a product with brand `apple`
- **THEN** the new product's brand is `Apple`

#### Scenario: Only product of a brand changes its case
- **WHEN** the only product with brand `apple` is updated with brand `Apple`
- **THEN** its brand becomes `Apple`

### Requirement: Brand suggestions
`GET /api/products/brands` SHALL return, without authentication, every brand used by stored products (active and inactive) once, grouped ignoring letter case with the most used spelling, each with its product count, ordered by name ignoring case.

#### Scenario: Brands with counts
- **WHEN** three products use `Apple` and one uses `Logitech`
- **THEN** the response lists `{ "name": "Apple", "productCount": 3 }` and `{ "name": "Logitech", "productCount": 1 }`

### Requirement: Cost price is the expected cost
`costPrice` SHALL be the expected unit cost that the owner enters to set the selling price and estimate the gross margin. Within Catalog only product creation and update SHALL change it, storing the value as sent; it is not the actual cost of a sale, which order lines keep in their own `unitCost`.

#### Scenario: Owner changes the expected cost
- **WHEN** an Admin updates a product with `costPrice` 17,000,000
- **THEN** the product detail returns `costPrice` 17,000,000

#### Scenario: Status change keeps the expected cost
- **WHEN** an Admin stops and resumes selling a product whose `costPrice` is 17,000,000
- **THEN** `costPrice` is still 17,000,000
