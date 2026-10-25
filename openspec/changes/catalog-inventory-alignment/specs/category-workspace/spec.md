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
