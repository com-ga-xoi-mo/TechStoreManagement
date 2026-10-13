# Spec Delta

## MODIFIED Requirements

### Requirement: Stock tab presents read-only balance rows
The client SHALL show a read-only stock table with one row per product and optional variant balance and SHALL provide a read-only action to open that balance's detail.

#### Scenario: View stock balances
- **WHEN** the user opens Tồn kho
- **THEN** each row identifies the product, optional variant, SKU, category, selling price, and stock status without mutation or stock-adjustment actions

#### Scenario: Open selected stock detail
- **WHEN** the user activates the detail action for a product or variant row
- **THEN** the client opens stock detail for that exact balance without changing inventory
