## ADDED Requirements

### Requirement: Workspace follows the app theme
The "Danh mục và Kho" workspace (Danh mục, Tồn kho and Serial/IMEI tabs) and every dialog it opens SHALL follow the app's Light/Dark theme setting, including a change made while the page is open. Text SHALL keep at least 4.5:1 contrast against its background in both themes, and status meaning SHALL also be given in words.

#### Scenario: Switch theme while the workspace is open
- **WHEN** the user opens the workspace, then switches Settings from Sáng to Tối and back
- **THEN** page background, table rows, badges, buttons, inputs and icons change with the theme and all text stays readable, without reopening the page

#### Scenario: Dialog in dark theme
- **WHEN** the app is in Tối and the user opens the create, edit or delete category dialog, or the import, adjust, detail or delete stock dialog
- **THEN** the dialog uses dark surfaces and light text, with readable labels, field errors and focus states

#### Scenario: Danger text contrast
- **WHEN** a destructive warning or field error is shown on a light surface
- **THEN** its text color has at least 4.5:1 contrast with that surface

### Requirement: Category row actions open from one menu
Each category row SHALL offer Edit and Delete through one “•••” button, labeled for assistive technology as “Thao tác cho <category name>”, that opens a menu with “Sửa danh mục” and “Xóa danh mục”, the latter in the danger color. The expand chevron, the status switch and the row hover background SHALL stay as they are.

#### Scenario: Edit or delete from the row menu
- **WHEN** the user opens the “•••” menu of the category row “Accessories” and picks “Sửa danh mục” or “Xóa danh mục”
- **THEN** the Edit Category Dialog or the Delete Category Dialog opens for “Accessories”, and the tree is unchanged until that operation succeeds

#### Scenario: Keyboard access
- **WHEN** the user moves focus to a row's “•••” button with Tab and presses Enter
- **THEN** the menu opens, the arrow keys move between its items, and Esc closes it without any action
