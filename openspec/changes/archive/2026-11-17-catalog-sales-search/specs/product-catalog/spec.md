# Spec Delta

## MODIFIED Requirements

### Requirement: Keyword search
The `q` parameter SHALL be trimmed and split on whitespace into at most 8 words. A product SHALL match when every word matches, case-insensitively and as a substring, at least one of the product name, product SKU, product brand, product barcode, product description, or the SKU, barcode or variant name of any of the product's variants. A product SHALL also match when the whole trimmed `q` equals, case-insensitively, either the serial number or the IMEI of one of its Serial/IMEI records. An empty `q` applies no keyword filter. The keyword filter SHALL combine with every other filter, and paging SHALL stay stable.

#### Scenario: Search by product name
- **WHEN** a client calls `GET /api/products?q=iphone`
- **THEN** products whose name contains "iPhone" in any letter case are returned

#### Scenario: Search by variant SKU
- **WHEN** a client calls `GET /api/products?q=IP15PM-256-NT`
- **THEN** the product owning the variant with SKU `IP15PM-256-NT` is returned once

#### Scenario: Search by brand
- **WHEN** a client calls `GET /api/products?q=logi`
- **THEN** products whose brand contains "Logi" in any letter case are returned, even when their name does not contain it

#### Scenario: Words in any position
- **WHEN** a product is named "MacBook Air M3" and a client calls `GET /api/products?q=macbook m3`
- **THEN** that product is returned

#### Scenario: Every word must match
- **WHEN** a client calls `GET /api/products?q=macbook samsung`
- **THEN** a product that matches only "macbook" is not returned

#### Scenario: Exact IMEI
- **WHEN** a Serial/IMEI record with `imei = 356789012345678` and no serial number belongs to product P and a client calls `GET /api/products?q=356789012345678`
- **THEN** P is returned once

#### Scenario: Partial IMEI does not match
- **WHEN** a client calls `GET /api/products?q=35678901`
- **THEN** P is not returned because of its serial

#### Scenario: Combined filters
- **WHEN** a client calls `GET /api/products?q=laptop&brand=Apple&minPrice=20000000&spec=ram_gb:16`
- **THEN** only products matching all four conditions are returned and `totalCount` counts only them

#### Scenario: Exact serial number
- **WHEN** a record has `serial_number = Serial-AbC123` and a null IMEI and a client calls `GET /api/products?q=serial-abc123`
- **THEN** its owning product is returned once, while a serial prefix alone does not match through that record
