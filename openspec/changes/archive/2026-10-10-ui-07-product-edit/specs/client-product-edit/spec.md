# Spec Delta

## Purpose

Lets authorized staff edit catalog product fields and variants through a preview-backed dialog while isolating unsaved drafts and leaving server-owned validation and inventory behavior authoritative.

## ADDED Requirements

### Requirement: Product edit opens with an isolated draft
The client SHALL let an authorized user open a product edit dialog populated with editable catalog fields and keep edits separate from the displayed catalog until save succeeds.

#### Scenario: Open edit dialog
- **WHEN** a preview user with the edit capability opens edit for a listed product
- **THEN** a native dialog shows the selected product's current name, category, brand, model/SKU and barcode, image URL, specifications, pricing fields permitted by the preview capability, and its existing variants

#### Scenario: Product cannot be loaded for editing
- **WHEN** the repository reports that a selected product is unavailable or fails to load
- **THEN** the dialog displays a readable unavailable/error state and does not show another product's data as the selected product

#### Scenario: Serial tracking remains unchanged
- **WHEN** a serial-tracked product is edited without an implemented server rule authorizing a tracking-mode change
- **THEN** the form shows the current mode as read-only and does not change inventory or Serial/IMEI records

### Requirement: Variant tab changes preserve unsaved values
The client SHALL preserve independent unsaved variant edits when the user switches between variant tabs in the edit dialog.

#### Scenario: Switch between variant tabs
- **WHEN** the user edits a variant's fields and selects another variant tab before saving
- **THEN** the first variant's entered SKU, barcode, prices, and specifications remain intact when the user returns to it

#### Scenario: Add or remove a draft variant
- **WHEN** the user adds a variant or removes a variant in the edit form
- **THEN** the change affects only the isolated draft, the remaining variants retain their values, and at least one variant remains required for save

### Requirement: Product edit validates fields and treats SKU suggestions as advisory
The client SHALL validate edit-form completeness and numeric values before save and SHALL distinguish local SKU suggestions from server validation.

#### Scenario: Invalid product or variant fields
- **WHEN** required values are missing, a price is malformed or negative, or a specification is incomplete or duplicated
- **THEN** save is blocked, readable field errors are shown, and focus moves to the first invalid field

#### Scenario: Request a variant SKU suggestion
- **WHEN** the user requests a suggested variant SKU
- **THEN** the client fills an editable suggestion derived from the product model and selected variant values, identifies it as a suggestion, and does not claim that it is unique or accepted by the server

#### Scenario: Preserve manual SKU changes
- **WHEN** the user edits a suggested SKU manually
- **THEN** changing tabs or other fields does not silently replace the manual value with another suggestion

### Requirement: Product edit save and cancel have explicit outcomes
The client SHALL commit preview edits only after a successful repository result and SHALL discard an unsaved draft when the dialog is cancelled or closed.

#### Scenario: Save succeeds
- **WHEN** valid edits are accepted by the preview repository
- **THEN** the dialog closes with a successful preview result and the shared preview catalog contains the saved product and variant values

#### Scenario: Cancel leaves the original product unchanged
- **WHEN** the user cancels or closes the dialog before a successful save
- **THEN** the draft is discarded and the original preview product, list cards, and detail data remain unchanged

#### Scenario: Save is pending
- **WHEN** a save operation is in progress
- **THEN** progress is visible and another save cannot be started

### Requirement: Edit failures and conflicts preserve the draft
The client SHALL show repository failures and conflicts without discarding entered edit values or pretending the catalog was updated.

#### Scenario: Duplicate identifier conflict
- **WHEN** the preview repository returns a duplicate SKU or barcode conflict
- **THEN** the dialog identifies the affected field, retains all draft values, and explains that only the server can determine global uniqueness

#### Scenario: Variant has existing transaction history
- **WHEN** a configured preview response reports that a variant change conflicts with existing transaction history
- **THEN** the dialog explains that the change was rejected, retains the draft for correction, and leaves the saved catalog unchanged

#### Scenario: General save failure
- **WHEN** a preview save fails for another reason
- **THEN** a readable error and retry path are shown, all draft values remain available, and no success result is emitted

### Requirement: Product edit respects preview capability signals and data provenance
The client SHALL use explicit preview capability signals for edit affordances and sensitive cost fields while treating the server as authoritative for real authorization.

#### Scenario: Edit capability is absent
- **WHEN** the current preview session has no product-edit capability
- **THEN** the product edit action is unavailable and the dialog cannot be opened through the normal UI flow

#### Scenario: Cost-write capability is absent
- **WHEN** the current preview session has no cost-write capability
- **THEN** cost-price fields are not exposed for editing and no cost value is implied by the client

#### Scenario: Preview data provenance
- **WHEN** edit data or save results come from a fake repository
- **THEN** the dialog and result identify preview/sample behavior and do not claim live persistence, server validation, or authorization

### Requirement: Product edit dialog remains operable at desktop sizes
The native edit dialog SHALL keep fields, variant tabs, feedback, and actions reachable at the user's current and narrower usable desktop widths and by keyboard.

#### Scenario: Narrow usable window
- **WHEN** the owner narrows the application window while the dialog is open
- **THEN** the dialog reflows or scrolls vertically without horizontally clipping product fields, variant tabs, row controls, feedback, Cancel, or Save

#### Scenario: Keyboard and validation focus
- **WHEN** the user operates the dialog with a keyboard and submits invalid input
- **THEN** fields, tabs, and row actions are reachable in a sensible Tab order, available actions respond to Enter/Space, Escape cancels according to the dialog pattern, and the first invalid field receives visible focus
