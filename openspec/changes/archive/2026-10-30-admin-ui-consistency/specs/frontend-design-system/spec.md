# Spec Delta

## ADDED Requirements

### Requirement: Consistent admin page headings
Admin pages SHALL present page titles and supporting descriptions with a consistent visual hierarchy and alignment.

#### Scenario: View common admin pages
- **WHEN** a user opens Products, Customers, Dashboard, Category and Inventory, or Settings
- **THEN** each page shows a clear title and any supporting description with consistent typography and spacing

### Requirement: Shared primary action presentation
Primary actions across admin pages SHALL use the shared primary color, typography, spacing, and corner shape; actions shown with a plus sign SHALL keep that sign visually balanced with the label.

#### Scenario: Compare add and search actions
- **WHEN** a user views primary actions on Products, Category and Inventory, Customers, or the Orders list toolbar
- **THEN** buttons use the shared primary appearance and plus signs are sized consistently with their labels

### Requirement: Compact semantic inventory status badges
Inventory and Serial/IMEI tables SHALL display status as compact text badges whose color communicates the corresponding status category.

#### Scenario: View inventory and device statuses
- **WHEN** a user views stock or Serial/IMEI rows
- **THEN** each status appears in a compact badge with readable status text and the appropriate semantic color

### Requirement: Aligned Settings content
The Settings page SHALL align its title and description and provide consistent horizontal insets for settings rows and application information.

#### Scenario: View Settings sections
- **WHEN** a user opens the Settings page and expands its information sections
- **THEN** the title appears above its description and the settings content aligns within consistent left and right margins
