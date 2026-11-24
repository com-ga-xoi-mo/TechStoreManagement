## ADDED Requirements

### Requirement: Loyalty summary in the customer profile
The customer profile SHALL show a loyalty summary from `GET /api/customers/{id}/loyalty-summary`: the points, the tier, the tier discount percent, the next tier with the points still missing, and a progress bar between the current tier threshold and the next one. At Platinum it SHALL say the top tier is reached. The summary SHALL be shown to every role that can open the profile.

#### Scenario: Silver customer
- **WHEN** a profile of a customer with 700 points opens
- **THEN** the card shows tier Silver, discount 2%, next tier Gold and 800 points missing

#### Scenario: Top tier
- **WHEN** a Platinum customer's profile opens
- **THEN** the card shows discount 10% and that the top tier is reached, with no missing points

#### Scenario: Sales opens a profile
- **WHEN** a SalesStaff session opens a profile
- **THEN** the summary card is shown while the history tabs stay hidden

### Requirement: Loyalty summary failure does not hide the profile
When the loyalty summary cannot be loaded, the card SHALL be hidden and the profile, tier and points from the profile response SHALL still be shown.

#### Scenario: Summary request fails
- **WHEN** the summary answers 500 and the profile answers 200
- **THEN** the profile is shown without the summary card and no error replaces the profile

### Requirement: Voucher entry from the customer list
The customer list SHALL show a "Voucher" button for Admin and Manager that opens the voucher page. It SHALL be hidden for SalesStaff.

#### Scenario: Manager opens vouchers
- **WHEN** a Manager presses Voucher on the customer list
- **THEN** the voucher page opens and Customers stays selected in the menu
