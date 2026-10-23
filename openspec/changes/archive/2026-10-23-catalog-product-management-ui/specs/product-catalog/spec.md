# Spec Delta

## MODIFIED Requirements

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
