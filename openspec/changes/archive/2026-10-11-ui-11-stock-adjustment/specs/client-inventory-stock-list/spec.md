# Spec Delta

## MODIFIED Requirements

### Requirement: Stock tab presents read-only balance rows
The client SHALL show a read-only stock table with one row per product and optional variant balance and SHALL provide dedicated actions to inspect details and open a type-aware stock adjustment without changing inventory directly in the row.

#### Scenario: View stock balances
- **WHEN** the user opens Tồn kho
- **THEN** each row identifies the product, optional variant, SKU, category, selling price, and stock status without inline stock mutation

#### Scenario: Open selected stock detail
- **WHEN** the user activates the detail action for a product or variant row
- **THEN** the client opens stock detail for that exact balance without changing inventory

#### Scenario: Open adjustment for selected balance
- **WHEN** the user activates the adjustment action for a product or variant row
- **THEN** the client opens the type-appropriate adjustment interaction for that exact balance and does not change inventory before repository success
