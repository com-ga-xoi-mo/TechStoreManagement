# Spec Delta

## Purpose

Allows authorized store staff to manage customer profiles and inspect persisted purchase and loyalty records from the customer screens aligned with the supplied Stitch design.

## ADDED Requirements

### Requirement: Customer list search, filtering, and Stitch metrics
The customer screen SHALL show paginated customers with identity, tier, points, total spending, and last completed purchase, and allow name/phone search plus tier and activity filtering. The list SHALL request active customers by default and offer `Active`, `Inactive`, and `All`. It SHALL omit the order-count column so the primary row action remains visible without horizontal scrolling.

#### Scenario: Search customer by name or phone
- **WHEN** an Admin enters a name or phone fragment
- **THEN** the list automatically requests and shows matching customers after the debounce and reflects loading, empty, or error state accurately

#### Scenario: Filter by tier
- **WHEN** an Admin selects a membership tier
- **THEN** the list requests and displays only customers in that tier while retaining the current search term

#### Scenario: Default to active customers
- **WHEN** the Admin first opens the customer screen
- **THEN** the list requests `isActive=true` and does not show inactive customers

#### Scenario: Filter by activity state
- **WHEN** an Admin selects inactive customers or all customers
- **THEN** the list requests `isActive=false` or omits `isActive` respectively while retaining the current search term and tier filter

#### Scenario: Show order aggregates
- **WHEN** the customer list is displayed
- **THEN** each row shows completed-order spending total and most recent completed purchase time without making per-customer detail requests

#### Scenario: View or edit a customer from the list
- **WHEN** an Admin activates the eye or pencil action on a customer row
- **THEN** the client opens that customer's details or edit form respectively, with both compact actions grouped beneath the centered action header and reachable without horizontal scrolling at the target width

#### Scenario: Present tier colors consistently
- **WHEN** a customer row or detail profile is displayed
- **THEN** Standard uses the information-blue palette, Silver the neutral-silver palette, Gold the warning-gold palette, and Platinum the accent-purple palette for both tier badge and avatar

#### Scenario: Align list headers and values
- **WHEN** the customer table is displayed
- **THEN** text columns align left, numeric columns align right, and the action column aligns center consistently between each header and its values

#### Scenario: Separate filters from the table
- **WHEN** the search and filters are displayed
- **THEN** tier and activity filters use matching rounded control shapes and retain visible spacing above the table

#### Scenario: Deactivate from customer details
- **WHEN** an Admin chooses to deactivate an active customer from the detail view
- **THEN** the client presents the existing explicit confirmation before calling the deactivation API

#### Scenario: Reset with existing controls
- **WHEN** an Admin empties the search field, selects `Tất cả hạng`, and selects `Đang hoạt động`
- **THEN** the list shows the default unfiltered active-customer result without requiring a separate clear-all button

### Requirement: Responsive search and fixed ordering
Search SHALL update after a short debounce while the Admin types, without requiring Enter or focus loss. Customer results SHALL retain the API's newest-created ordering. The UI SHALL NOT show a sort selector while no alternative sort exists.

#### Scenario: Search while typing
- **WHEN** an Admin edits the search text and pauses briefly
- **THEN** the newest text is submitted automatically and any obsolete request is cancelled or ignored

#### Scenario: Fixed newest-created ordering
- **WHEN** the customer list is displayed
- **THEN** customers use the API's newest-created ordering and no one-option sort dropdown is shown

### Requirement: Create and update customer profiles
The customer UI SHALL allow an Admin to create a customer and edit that customer's contact information using the API-supported fields and Stitch forms.

#### Scenario: Create a valid customer
- **WHEN** an Admin submits a valid name, phone, and optional profile details
- **THEN** the client sends the create request, shows the returned profile, and refreshes the customer list

#### Scenario: Show server validation and duplicate errors
- **WHEN** the API rejects a create or update request
- **THEN** the UI presents field validation errors or a safe conflict message and preserves the entered values for correction

#### Scenario: Update customer contact details
- **WHEN** an Admin edits and saves a customer's profile
- **THEN** the client updates the displayed contact details from the API response without changing points, tier, or activity state

#### Scenario: Correct an inactive customer profile
- **WHEN** an Admin edits an inactive customer's contact information
- **THEN** the client saves the correction without reactivating or deleting the customer

#### Scenario: Enter or choose a customer's date of birth
- **WHEN** an Admin creates or edits a customer profile
- **THEN** the Admin can enter the date in `dd/MM/yyyy` format or choose it with the black calendar button on the left side of the field, and the form submits the same date

### Requirement: View customer profile and loyalty transaction history
The customer detail view SHALL display persisted profile details, membership tier, activity state, point balance, and paginated point transactions without changing loyalty data.

#### Scenario: View persisted point balance and history
- **WHEN** an Admin opens the loyalty history in customer details
- **THEN** the UI displays the API's current point balance and persisted point events, including point change, balance after, reason, timestamp, and related order when present

#### Scenario: Loyalty data is read-only
- **WHEN** an Admin views customer loyalty information
- **THEN** the UI offers no action to calculate, award, redeem, reverse, or otherwise mutate points

### Requirement: View customer purchase history
The customer detail view SHALL show the selected customer's paginated order history using the order data returned by the customer API. The primary product category SHALL appear as secondary text beneath the product name. The order amount header SHALL align with right-aligned amount values, and the status header SHALL align with centered status badges.

#### Scenario: View purchase history
- **WHEN** an Admin opens the purchase-history tab
- **THEN** the UI displays that customer's order code, primary product and category, status, total amount, and timestamps in API order

#### Scenario: Distinguish completed and cancelled orders
- **WHEN** the order history includes both `Completed` and `Cancelled` orders
- **THEN** completed status uses a success color and cancelled status uses a danger color; cancelled orders remain absent from purchased-device history

#### Scenario: Customer has no orders
- **WHEN** the API returns an empty order page
- **THEN** the UI shows an explicit empty-history state without fabricated rows

### Requirement: View purchased devices and identifiers
The customer detail view SHALL show a paginated history of devices purchased in completed orders, including product/variant, serial or IMEI, and order reference when returned by the customer API. The history table SHALL use consistent column widths between its header and every data row.

#### Scenario: View purchased devices
- **WHEN** an Admin opens the purchased-devices tab
- **THEN** the UI displays the customer's completed-order devices with their product, variant, category-appropriate product icon, linked serial/IMEI, and order reference when available

#### Scenario: Customer has no purchased devices
- **WHEN** the API returns an empty purchased-device page
- **THEN** the UI shows an explicit empty state without fabricated device or identifier data

### Requirement: Show warranty and device status accurately
The customer detail view SHALL show warranty and device status when provided by the API. It SHALL identify non-serial-tracked purchases as not having per-unit warranty or device tracking data instead of displaying fabricated values; this does not assert that the product has no manufacturer warranty. A linked `Sold` device SHALL be presented to the customer as “Đang sử dụng”.

#### Scenario: Show warranty and device status for a linked device
- **WHEN** a completed order item is linked to a tracked serial/IMEI with warranty and status data
- **THEN** the UI displays its serial/IMEI, warranty dates/status, and current device status in aligned columns, with in-warranty text green and expired text red

#### Scenario: Show non-serial-tracked purchase
- **WHEN** a completed order item has no linked serial/IMEI
- **THEN** the UI identifies per-unit warranty and device tracking as unavailable, displays the tracking label in red, and does not invent identifiers or an inventory device status

### Requirement: Manage customer activity safely
The customer UI SHALL let an Admin deactivate an active customer only after explicit confirmation, reactivate an inactive customer, and communicate success or failure without implying permanent deletion. It SHALL expose no permanent-delete action.

#### Scenario: Confirm deactivation
- **WHEN** an Admin confirms the “Vô hiệu hóa” action
- **THEN** the client calls the API deactivation operation, marks the profile inactive, and refreshes the list while retaining customer and history data

#### Scenario: Cancel deactivation
- **WHEN** an Admin dismisses or cancels the confirmation
- **THEN** no deactivation request is sent and the profile remains unchanged

#### Scenario: Reactivate an inactive customer
- **WHEN** an Admin chooses `Kích hoạt lại` for an inactive customer
- **THEN** the client calls the activation API, marks the profile active, and refreshes the list without changing profile or history data

#### Scenario: Activity-specific actions
- **WHEN** an Admin views an active or inactive customer
- **THEN** an active customer offers deactivation, an inactive customer offers reactivation, and neither state offers permanent deletion

### Requirement: Customer UI lifecycle and accessible states
Customer pages and dialogs SHALL use existing C# Markup frontend patterns, match the supplied Stitch customer screens, support keyboard interaction, and present loading, empty, validation, and recoverable error states without fake customer data. View state and editable inputs SHALL be connected to ViewModels using typed C# Markup bindings. Views MUST NOT call customer repositories directly or construct feature ViewModels; ViewModels SHALL access API data only through client repositories.

#### Scenario: Keyboard access and request failure
- **WHEN** an Admin uses the customer workflows with a keyboard and an API request fails
- **THEN** actions remain keyboard accessible, focus remains usable, and the UI shows a recoverable error with retry where appropriate

#### Scenario: Leave customer page during a request
- **WHEN** the Admin navigates away while customer data is loading
- **THEN** the obsolete request is cancelled or ignored and cannot overwrite the next activation's state

### Requirement: Dedicated customer dialog views
Customer list, create/edit form, and customer-detail modal contents SHALL be implemented as dedicated C# Markup View classes. The list page MAY coordinate modal lifetime and focus, but SHALL NOT own the detail/form visual trees or synchronize view state through property-change or control event handlers.

#### Scenario: Open customer detail
- **WHEN** an Admin opens a customer's detail view
- **THEN** the page hosts the dedicated dialog View, which binds its DI-resolved ViewModel without calling repositories or manually synchronizing UI state
