# Spec Delta

## MODIFIED Requirements

### Requirement: Inventory workspace keeps category and unfinished inventory tabs distinct
The client SHALL provide a persistent-shell inventory workspace with a category tab, a preview-backed read-only stock tab, and an explicit unavailable state for the unfinished Serial/IMEI tab.

#### Scenario: Open the inventory destination
- **WHEN** a preview user opens Danh mục & Kho from the Shell
- **THEN** the Shell remains visible, the inventory workspace opens with Danh mục selected, and the category tree is loaded independently of product-list queries

#### Scenario: Select the stock tab
- **WHEN** the user selects Tồn kho
- **THEN** the stock list loads independently through its preview source while the category tree behavior remains available when the user returns to Danh mục

#### Scenario: Select an unfinished stock or Serial/IMEI tab
- **WHEN** the user selects Tồn kho or Serial / IMEI
- **THEN** Tồn kho shows its preview-backed read-only stock list, while Serial / IMEI shows a clear not-yet-implemented state with no fabricated Serial/IMEI counts or actions

#### Scenario: Return to the category tab
- **WHEN** the user returns to Danh mục
- **THEN** the category tree state remains coherent and the client does not reload it unnecessarily while its current snapshot is valid
