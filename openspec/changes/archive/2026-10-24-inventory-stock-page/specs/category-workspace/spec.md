# Spec Delta

## MODIFIED Requirements

### Requirement: Category workspace layout
The workspace SHALL present the header, underline tabs, filter toolbar, tree table, and footer in the order and visual structure of `docs/designs/category-list.html`. Explicit numeric and text values in the change request SHALL take precedence where they differ from the HTML. Category feature UI SHALL introduce no XAML files.

#### Scenario: Open the category workspace
- **WHEN** the user opens the category workspace
- **THEN** it shows “Danh mục và Kho”, a data-bound category total, and “Thêm danh mục” with an add icon
- **AND** “Danh mục” is active with a blue underline and count badge, while “Tồn kho” and “Serial / IMEI” retain inactive styling until selected
- **AND** selecting “Tồn kho” activates the inventory-stock workspace in the same feature shell without adding a seventh Shell destination
- **AND** selecting “Serial / IMEI” retains its inactive/deferred state until its own capability is implemented

#### Scenario: Apply requested visual tokens
- **WHEN** the workspace is rendered
- **THEN** its background is `#F8FAFC`, the white card has a 12-unit radius, `#E2E8F0` border, and subtle shadow, and primary controls use `#0066FF` with `#1D4ED8` hover
- **AND** primary and secondary text use `#0F172A` and `#64748B`, with the HTML typography hierarchy and spacing

