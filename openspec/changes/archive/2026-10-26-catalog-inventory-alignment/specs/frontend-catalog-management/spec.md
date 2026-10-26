## ADDED Requirements

### Requirement: Editor category picker offers only active categories
The product editor's category picker SHALL list only categories whose `isActive` is true, keeping the tree path label. When an edited product currently belongs to a hidden category, the picker SHALL also list that category, labelled "(đang ẩn)", and the editor SHALL tell the user that the product can still be saved there but no other product can be moved into it.

#### Scenario: Create with hidden categories present
- **WHEN** the category tree contains active and hidden categories and the user opens the create editor
- **THEN** the picker lists only the active categories, each with its full path

#### Scenario: Edit a product in a hidden category
- **WHEN** the user opens the editor for a product whose category is hidden
- **THEN** the picker shows that category selected with the suffix "(đang ẩn)", the other hidden categories stay out of the list, and an informational hint under the picker says the category is hidden and the product can still be saved

#### Scenario: Save without changing the hidden category
- **WHEN** the user changes the price of that product and saves without choosing another category
- **THEN** the client sends one `PUT` with the same `categoryId`, the save succeeds and the detail page shows the new price

#### Scenario: Move out of the hidden category
- **WHEN** the user picks an active category instead and saves
- **THEN** the hint disappears as soon as the active category is picked, and the `PUT` carries the new `categoryId`
