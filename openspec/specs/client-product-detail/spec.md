# client-product-detail Specification

## Purpose

Lets staff inspect one catalog product and its valid variants in a showroom dialog, while keeping preview information distinct from authoritative catalog, stock, cost, and permission data.

## Requirements

### Requirement: Product showroom presents the selected product detail
The client SHALL present the selected product's identity, image, product and variant specifications, selling price, and available stock in a dismissible showroom dialog.

#### Scenario: Open showroom from the product list
- **WHEN** a user activates a product in the implemented Catalog list
- **THEN** a native dialog opens for that product while retaining the list beneath it

#### Scenario: Load product detail
- **WHEN** a detail request is pending, succeeds, returns no product, or fails
- **THEN** the dialog shows a distinct loading, populated, unavailable, or error state without presenting stale or fabricated values as the selected product

#### Scenario: Retry failed detail load
- **WHEN** the user retries a failed detail request
- **THEN** the same product is requested again and the dialog remains open with a clear loading/result state

#### Scenario: Close showroom
- **WHEN** the user activates Close or presses Escape
- **THEN** the dialog closes and returns to the same product-list context without changing list filters or page

#### Scenario: Preview provenance
- **WHEN** detail data comes from the mock repository
- **THEN** the dialog visibly identifies it as preview data and does not claim that product, price, stock, or permission values are authoritative

### Requirement: Variant choices represent only existing product variants
The client SHALL offer only combinations present in the selected product's variant data and SHALL keep all variant-dependent information synchronized with the selected variant.

#### Scenario: Select a valid configuration and color
- **WHEN** the user selects an available storage/color combination
- **THEN** the selected SKU, selling price, available quantity, stock status, and variant-specific specifications update together from that variant

#### Scenario: Unavailable combination
- **WHEN** a storage/color combination does not exist or is unavailable for selection
- **THEN** the user cannot select it and the current variant values remain unchanged

#### Scenario: Product with one variant
- **WHEN** a product has one valid variant
- **THEN** its values are shown without requiring a meaningless variant choice

### Requirement: Cost information respects the current session capability
The client SHALL display average cost and estimated margin only when the current session has the cost-read capability and the selected detail response supplies those values.

#### Scenario: Authorized preview session with cost data
- **WHEN** the session includes `catalog:cost-read` and the selected variant has cost data
- **THEN** the dialog displays the supplied cost and margin values for that variant

#### Scenario: Session without cost capability
- **WHEN** the session does not include `catalog:cost-read`
- **THEN** cost and margin values are absent from the visible and accessible UI, even if a preview fixture contains them

#### Scenario: Authorized session without cost data
- **WHEN** the session has `catalog:cost-read` but the detail response omits cost data
- **THEN** no cost or margin value is fabricated or calculated by the client

#### Scenario: Permission source is preview-only
- **WHEN** a mock session grants or withholds the cost capability
- **THEN** that fixture controls only the preview UI and does not claim to enforce or prove server authorization

### Requirement: Product detail stays within showroom scope
The showroom SHALL expose only product inspection and variant selection actions in this change.

#### Scenario: Edit, discontinue, and Serial/IMEI actions are out of scope
- **WHEN** the showroom is displayed
- **THEN** it does not expose working edit, discontinue, inventory, or Serial/IMEI destinations unless those destinations are implemented in a later change

### Requirement: Showroom remains usable across desktop window sizes and input methods
The dialog SHALL keep its content and available interactions usable at normal and narrower desktop widths and through keyboard input.

#### Scenario: Narrow usable desktop window
- **WHEN** the owner narrows the desktop window while the showroom is open
- **THEN** dialog content reflows or scrolls vertically without horizontal clipping of product identity, specifications, variants, price, stock, permission-gated data, or Close

#### Scenario: Keyboard operation
- **WHEN** the user navigates the dialog with a keyboard
- **THEN** variant choices and retry/close controls are reachable in a sensible Tab order, activation works with Enter/Space, Escape closes the dialog, and focus remains visible
