# Spec Delta

## MODIFIED Requirements

### Requirement: Placeholder boundaries
The client SHALL label destinations that have no implemented business page and SHALL not represent placeholder content as live business data.

#### Scenario: Unimplemented destination
- **WHEN** the user opens Sản phẩm, Đơn hàng, Khách hàng, or the Tồn kho or Serial / IMEI tab before its screen is implemented
- **THEN** the destination identifies the unavailable feature and does not show invented business data or actions

#### Scenario: Inventory category destination
- **WHEN** the user opens Danh mục & Kho after the category-tree screen is installed
- **THEN** the Shell remains visible and its content shows the category/inventory tab workspace with the category tree selected by default

#### Scenario: Settings unavailable
- **WHEN** the user inspects or activates Cài đặt before a settings design and route exist
- **THEN** it remains visibly unavailable and does not open a fabricated settings page
