# Spec Delta

## Purpose

Lets staff browse a hierarchical catalog category tree in the inventory workspace while keeping stock, Serial/IMEI, and category mutation workflows separate until their own screens and API contracts are ready.

## ADDED Requirements

### Requirement: Inventory workspace keeps category and unfinished inventory tabs distinct
The client SHALL provide a persistent-shell inventory workspace with a category tab and explicit unavailable states for unfinished inventory tabs.

#### Scenario: Open the inventory destination
- **WHEN** a preview user opens Danh mục & Kho from the Shell
- **THEN** the Shell remains visible, the inventory workspace opens with Danh mục selected, and the category tree is loaded independently of product-list queries

#### Scenario: Select an unfinished stock or Serial/IMEI tab
- **WHEN** the user selects Tồn kho or Serial / IMEI before those screens are implemented
- **THEN** the selected tab shows a clear not-yet-implemented state and no fabricated stock or Serial/IMEI counts or actions

#### Scenario: Return to the category tab
- **WHEN** the user returns to Danh mục
- **THEN** the category tree state remains coherent and the client does not reload it unnecessarily while its current snapshot is valid

### Requirement: Category tree loading states are explicit and preview data is identified
The client SHALL distinguish category-tree loading, populated, empty, and failed results and SHALL identify fake repository data as preview data.

#### Scenario: Load the category tree
- **WHEN** the category tab is selected and the repository query is pending
- **THEN** loading feedback is visible and category rows are not presented as a completed result

#### Scenario: Category tree is populated
- **WHEN** the repository successfully returns category nodes
- **THEN** the hierarchy is displayed and the UI identifies the data as preview/sample when it came from the fake repository

#### Scenario: Category tree is empty
- **WHEN** the repository successfully returns no categories
- **THEN** a readable empty state is shown without sample rows or a loading indicator

#### Scenario: Category tree query fails
- **WHEN** the repository reports a load failure
- **THEN** a readable error and retry action are shown, and the client does not represent the failure as an empty category tree

#### Scenario: Retry category-tree loading
- **WHEN** the user retries after a category-tree failure
- **THEN** the retry uses the category repository and replaces the error state only after a successful response

### Requirement: Category hierarchy and counts are displayed without client-side inventory inference
The client SHALL display parent and child categories, descriptions, active status, and repository-provided product counts without deriving counts from the currently displayed or paged product results.

#### Scenario: Display category rows
- **WHEN** a category tree is shown
- **THEN** each row identifies its category name, parent or root status, optional description, active/inactive status, and count value supplied for that category by the repository

#### Scenario: Expand and collapse a parent
- **WHEN** the user activates a parent category's expand/collapse control
- **THEN** only that parent's descendants are hidden or shown, with the expanded state visibly and accessibly communicated

#### Scenario: Preserve category counts
- **WHEN** product results are filtered or paged elsewhere in the client
- **THEN** category counts remain the category counts supplied by the category repository and are not recomputed from that partial product result set

#### Scenario: Filter categories by name and active state
- **WHEN** the user searches category names or selects an active-state filter
- **THEN** only matching categories are shown, matching descendants retain their ancestor context, and the filter does not mutate category data

#### Scenario: Category data is read-only in this change
- **WHEN** the user inspects a category row
- **THEN** name, description, count, and active status are read-only, with no create, edit, delete, or status-toggle action

### Requirement: Category tree remains usable by keyboard and at usable desktop widths
The client SHALL keep category tabs, filters, tree rows, feedback, and hierarchy controls readable and operable by keyboard at the user's normal and narrower usable desktop widths.

#### Scenario: Narrow usable window
- **WHEN** the owner narrows the application window while viewing the category tree
- **THEN** the workspace reflows or scrolls without horizontally clipping tabs, filters, category names, parent labels, counts, status, or feedback

#### Scenario: Keyboard tree operation
- **WHEN** the user operates category tabs, filters, retry, and expand/collapse controls by keyboard
- **THEN** controls have a sensible Tab order, Enter or Space activates available actions, and focus remains visible
