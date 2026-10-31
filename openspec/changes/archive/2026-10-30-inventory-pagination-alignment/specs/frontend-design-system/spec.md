# Spec Delta

## ADDED Requirements

### Requirement: Consistent inventory pagination
Pagination in the Category, Inventory, and Serial/IMEI views SHALL use the same labeled navigation pattern as the Customers list.

#### Scenario: Navigate inventory pages
- **WHEN** a user views a paginated Category, Inventory, or Serial/IMEI list
- **THEN** the footer presents “Trang trước”, the current page and page count, and “Trang sau”, with unavailable directions disabled
