# client-product-create Specification

## Purpose

Lets staff create a catalog product through a preview-backed form while making validation and the boundary between catalog creation and inventory receipt explicit.

## Requirements

### Requirement: Product creation form captures catalog identity and variants
The client SHALL let staff enter product identity, category, prices, image URL, dynamic specifications, and one or more variants in a native desktop form.

#### Scenario: Open create form
- **WHEN** a preview session opens product creation from the product list
- **THEN** a native dialog shows fields for required product name, brand, category, product SKU, optional barcode, serial-tracking choice, selling and reference cost prices, optional image URL, dynamic specifications, and variant rows

#### Scenario: Edit dynamic specifications and variants
- **WHEN** a user adds, edits, or removes a specification or variant row
- **THEN** the form retains each row's entered key/value or variant name, SKU, optional barcode, price, and variant-specific specifications until the form is submitted or cancelled

#### Scenario: Category choices load
- **WHEN** the create dialog requests category choices
- **THEN** it shows available preview categories or a readable loading/error state and never represents preview categories as live server data

### Requirement: Product creation validates input and reports failures
The client SHALL validate product form input before submission and show field-level and submission feedback without discarding user input.

#### Scenario: Required and numeric fields are invalid
- **WHEN** required identity/category fields are missing, prices are malformed or negative, or required variant values are incomplete
- **THEN** submission is blocked and each invalid field has a readable validation message and accessible association

#### Scenario: Duplicate identifiers are reported as provisional
- **WHEN** a preview submission returns a duplicate SKU or barcode conflict
- **THEN** the form identifies the relevant field, preserves all entered values, and explains that final uniqueness is determined by the server during later API integration

#### Scenario: Submission succeeds
- **WHEN** valid form data is accepted by the preview repository
- **THEN** the dialog closes, a success result is shown, and the product list refreshes while retaining its current search/filter/sort context

#### Scenario: Submission fails
- **WHEN** the preview repository returns a configured failure
- **THEN** the dialog remains open, a readable error is shown, and all entered form values remain available for correction or retry

#### Scenario: Prevent duplicate submission
- **WHEN** a create request is pending
- **THEN** submission is visibly in progress and another create request cannot be started until it completes or is cancelled

#### Scenario: Cancel without writing
- **WHEN** the user cancels or closes the dialog before successful submission
- **THEN** no product is added to the preview catalog and the existing product-list context remains intact

### Requirement: Preview creation is clearly distinguished from live catalog data
The client SHALL identify preview-backed categories and create results as sample data and SHALL NOT claim that a mock operation changed the authoritative server catalog.

#### Scenario: Preview create provenance
- **WHEN** the create form or success result is backed by a fake repository
- **THEN** the UI identifies it as preview/sample behavior and does not claim a real product was persisted

#### Scenario: No silent live fallback
- **WHEN** a preview operation fails
- **THEN** the client reports the failure and does not silently switch to an HTTP data source

### Requirement: Product creation does not mutate inventory or save unsupported drafts
The create form SHALL keep catalog creation separate from inventory receipt and SHALL NOT expose unsupported stock or draft persistence actions.

#### Scenario: Create product without initial inventory
- **WHEN** a user creates a product through this form
- **THEN** the operation does not add stock, create an inventory movement, or accept an initial quantity as proof of stock

#### Scenario: Serial-tracked product has no quantity-only receipt
- **WHEN** a product is marked as serial/IMEI tracked
- **THEN** the create form does not let a quantity alone create stock; each physical unit must later be received through an inventory contract that accepts its device identifier

#### Scenario: Inventory receipt dependency is unavailable
- **WHEN** no implemented inventory-receipt screen and contract are available after product creation
- **THEN** the UI clearly states that inventory receipt is a separate pending workflow and does not navigate to a fake-success route

#### Scenario: Draft persistence is unsupported
- **WHEN** the create form is displayed without a draft-save contract
- **THEN** it has no working “Lưu nháp” action and closing the dialog discards only the unsaved local form state

### Requirement: Product creation form remains usable across desktop sizes and keyboard input
The native dialog SHALL keep form fields, validation, and actions reachable at the user's normal or narrower usable desktop width and by keyboard.

#### Scenario: Narrow usable desktop window
- **WHEN** the owner narrows the window while the dialog is open
- **THEN** the form reflows or scrolls vertically without horizontal clipping of identity, pricing, specifications, variants, feedback, Cancel, or Submit

#### Scenario: Keyboard operation and validation focus
- **WHEN** the user operates the dialog with a keyboard and submits invalid input
- **THEN** fields and row actions are reachable in a sensible Tab order, Enter/Space activates available actions, Escape closes or prompts according to the existing dialog pattern, and focus moves to the first invalid field while remaining visible
