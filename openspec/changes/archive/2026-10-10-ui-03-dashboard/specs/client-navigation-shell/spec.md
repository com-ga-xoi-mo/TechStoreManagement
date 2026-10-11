# Spec Delta

## MODIFIED Requirements

### Requirement: Persistent five-destination shell
The client SHALL define exactly five primary destinations and show the session-available subset in a persistent branded shell with a content region independent of the selected destination.

#### Scenario: Initial shell
- **WHEN** an explicit preview login opens the Shell after UI-03 is installed
- **THEN** it shows Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng in that order, selects Tổng quan, and displays the Dashboard page inside the Shell

#### Scenario: No extra business destination
- **WHEN** the primary sidebar is inspected
- **THEN** it contains no separate POS or Identity destination; the existing UI-00 theme preview is not shown as a sixth business item

#### Scenario: Footer
- **WHEN** the shell is displayed
- **THEN** it shows Cài đặt in the footer as unavailable and shows session status without presenting sample Admin TechStore data as a real authenticated user

#### Scenario: Other unfinished destinations
- **WHEN** a user selects Sản phẩm, Danh mục và Kho, Đơn hàng or Khách hàng before its own UI change is implemented
- **THEN** the Shell keeps its existing explicit placeholder for that destination and can return to Dashboard without losing navigation state
