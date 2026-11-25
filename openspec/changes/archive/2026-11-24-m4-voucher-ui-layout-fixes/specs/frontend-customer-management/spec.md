## MODIFIED Requirements

### Requirement: Loyalty summary in the customer profile
The customer profile SHALL show a loyalty summary from `GET /api/customers/{id}/loyalty-summary`: the points, the tier, the tier discount percent, the next tier with the points still missing, and a progress bar between the current tier threshold and the next one. At Platinum it SHALL say the top tier is reached. The summary SHALL be shown to every role that can open the profile. The summary SHALL be one compact strip, so that for Admin and Manager the history tabs stay inside the visible area of the profile dialog without scrolling, in a window as small as 800×600.

#### Scenario: Silver customer
- **WHEN** a profile of a customer with 700 points opens
- **THEN** the card shows tier Silver, discount 2%, next tier Gold and 800 points missing

#### Scenario: Top tier
- **WHEN** a Platinum customer's profile opens
- **THEN** the card shows discount 10% and that the top tier is reached, with no missing points

#### Scenario: Sales opens a profile
- **WHEN** a SalesStaff session opens a profile
- **THEN** the summary card is shown while the history tabs stay hidden

#### Scenario: History tabs stay in view
- **WHEN** an Admin opens a profile in an 800×600 window
- **THEN** the loyalty strip and the "Lịch sử mua hàng" tab are both inside the visible area of the dialog without scrolling
