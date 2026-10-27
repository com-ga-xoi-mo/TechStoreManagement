# Spec Delta: category-workspace

## MODIFIED Requirements

### Requirement: Deferred category action hooks
Add controls SHALL launch the Create Category Dialog. Edit and Delete controls SHALL continue to expose dialog-opening requests with the selected category identity for Edit/Delete without modifying state.

#### Scenario: Request an action
- **WHEN** the user activates Add, a row's Edit, or a row's Delete action
- **THEN** Add opens the Create Category Dialog, while Edit and Delete invoke their corresponding hooks with the target category identity
- **AND** the category tree remains unchanged until a valid category creation is submitted and completed

## ADDED Requirements

### Requirement: Create Category Dialog interaction
The Create Category Dialog SHALL validate category creation inputs, automatically generate URL-safe slugs from Vietnamese names in real time, submit valid data to the category API, and refresh the workspace tree upon successful creation.

#### Scenario: Real-time Vietnamese slug generation
- **WHEN** the user types or modifies the category name (e.g., "Tai nghe & Âm thanh")
- **THEN** the dialog displays an auto-generated lowercase, unaccented, hyphen-separated slug in the preview badge (e.g., "tai-nghe-am-thanh") in real time

#### Scenario: Parent category hierarchy selection
- **WHEN** the user opens the parent category dropdown
- **THEN** the options include "-- Danh mục gốc (Cấp 1) --" (value null) and all existing categories with hierarchy indication
- **AND** selecting a parent links the new category under that parent upon creation

#### Scenario: Successful category creation without keep-creating
- **WHEN** the user submits valid category data and "Tiếp tục thêm danh mục khác sau khi lưu" is unchecked
- **THEN** the system sends a CreateCategoryRequest containing only Name, Slug, ParentId, Description, and IsActive to the backend
- **AND** upon HTTP 201 response, the dialog closes and the category workspace tree automatically refreshes to display the new category

#### Scenario: Successful category creation with keep-creating
- **WHEN** the user submits valid category data and "Tiếp tục thêm danh mục khác sau khi lưu" is checked
- **THEN** upon HTTP 201 response, the category workspace tree refreshes in the background, the form fields reset to clean defaults, and the dialog remains open for subsequent entries

#### Scenario: Backend validation or conflict failure
- **WHEN** the backend returns an error (such as HTTP 409 Conflict for duplicate slug or HTTP 400 Bad Request)
- **THEN** the dialog remains open, existing user input is preserved without loss, and an informative error message is displayed

### Requirement: Create Category Dialog visual structure
The Create Category Dialog SHALL be implemented entirely in C# Markup without XAML, styled as a 640px Fluent modal with rounded corners, custom header, system tip box, and active POS status switch.

#### Scenario: Modal visual elements presentation
- **WHEN** the Create Category Dialog is rendered
- **THEN** it displays a 12px rounded container with maximum width 640px, a header with folder icon, title, subtitle, close button, required name field with slug badge, parent combobox with hint text, multi-line description field, POS active toggle switch, system tip box with info icon, keep-creating checkbox, cancel button, and primary add button
- **AND** no `.xaml` file is introduced for the dialog
