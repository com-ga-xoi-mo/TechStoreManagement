# Spec Delta

## MODIFIED Requirements

### Requirement: Product write authorization
`POST /api/products`, `PUT /api/products/{id}`, `PATCH /api/products/{id}/status` and `DELETE /api/products/{id}` SHALL require a valid bearer token whose role is `Admin` or `Manager`. A request without a valid token SHALL return 401 and a request from another role SHALL return 403, in both cases without changing data. All product read endpoints SHALL require authentication with any one of the four system roles.

#### Scenario: Missing token
- **WHEN** a client calls `PUT /api/products/{id}` without a bearer token
- **THEN** the response is 401 and the product is unchanged

#### Scenario: Sales staff cannot delete
- **WHEN** a user with role `SalesStaff` calls `DELETE /api/products/{id}`
- **THEN** the response is 403 and the product still exists

#### Scenario: Manager can update
- **WHEN** a user with role `Manager` sends a valid update
- **THEN** the response is 200

#### Scenario: Reads require login
- **WHEN** a client calls any product read endpoint without a valid token
- **THEN** the response is 401 instead of product data

#### Scenario: Reads stay open
- **WHEN** any of the four roles calls a product read endpoint with a valid current token
- **THEN** the existing successful response contract is preserved

### Requirement: Specification filter suggestions
`GET /api/products/spec-keys` SHALL return, for authenticated users with any one of the four system roles, each spec key that occurs in the effective specs of stored products (active and inactive), with the number of distinct products having it and its distinct scalar values. Each value SHALL carry a display `label`, its product count and a `filterValue` that, sent as `spec=<key>:<filterValue>`, matches exactly that value. An optional `categoryId` SHALL limit the products to that category only.

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

#### Scenario: Anonymous suggestion request
- **WHEN** a caller requests this endpoint without a valid token
- **THEN** the response is 401 and contains no catalog data

### Requirement: Specification definitions
The `spec_definitions` table SHALL hold one row per spec key with a display `label` (unique ignoring letter case), an optional `unit` and a `valueKind` (`number`, `string` or `boolean`), seeded with the keys used by the catalog seed data. `GET /api/products/spec-definitions` SHALL return all definitions for authenticated users with any one of the four system roles, ordered by label, and `GET /api/products/spec-keys` SHALL add `label`, `unit` and `valueKind` to every key that has a definition.

#### Scenario: Seeded definition
- **WHEN** a client calls `GET /api/products/spec-definitions` on a fresh database
- **THEN** the list contains `{ "key": "ram_gb", "label": "RAM", "unit": "GB", "valueKind": "number" }`

#### Scenario: Suggestion carries the label
- **WHEN** a client calls `GET /api/products/spec-keys` and `ram_gb` has a definition
- **THEN** the `ram_gb` entry has `label` `RAM` and `unit` `GB`; a key without a definition has null `label`, `unit` and `valueKind`

#### Scenario: Anonymous suggestion request
- **WHEN** a caller requests this endpoint without a valid token
- **THEN** the response is 401 and contains no catalog data

### Requirement: Brand suggestions
`GET /api/products/brands` SHALL return, for authenticated users with any one of the four system roles, every brand used by stored products (active and inactive) once, grouped ignoring letter case with the most used spelling, each with its product count, ordered by name ignoring case.

#### Scenario: Brands with counts
- **WHEN** three products use `Apple` and one uses `Logitech`
- **THEN** the response lists `{ "name": "Apple", "productCount": 3 }` and `{ "name": "Logitech", "productCount": 1 }`

#### Scenario: Anonymous suggestion request
- **WHEN** a caller requests this endpoint without a valid token
- **THEN** the response is 401 and contains no catalog data
