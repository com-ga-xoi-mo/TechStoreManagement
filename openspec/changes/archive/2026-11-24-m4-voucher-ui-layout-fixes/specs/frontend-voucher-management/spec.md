## ADDED Requirements

### Requirement: Voucher table columns fit their values
Each column of the voucher table SHALL be wide enough for its header and its longest value: the status badge, the discount with its cap, the minimum order, the uses, the expiry date and the actions header. The header and all rows SHALL share the same column widths and stay aligned. A discount or minimum order longer than its column SHALL continue on a second line.

#### Scenario: Window 1024 by 768
- **WHEN** the voucher page is open in a 1024×768 window
- **THEN** every column, including the row actions, is visible and no value or header is cut

#### Scenario: Long discount text
- **WHEN** a discount label is longer than its column
- **THEN** it continues on a second line and the other cells of the row stay aligned with the header

#### Scenario: Text contrast in both themes
- **WHEN** the page is shown in the Light theme or in the Dark theme
- **THEN** the text of the table, the editor dialog and the customer profile has the contrast of WCAG AA (4.5:1 for text, 3:1 for icons and large text), apart from controls that are disabled

### Requirement: Voucher table scrolls sideways when the window is narrow
When the window is narrower than the table needs, the header and the rows SHALL scroll sideways together instead of cutting text. When the window is wide enough, no sideways scroll SHALL appear.

#### Scenario: Window 800 by 600
- **WHEN** the voucher page is open in an 800×600 window
- **THEN** the table scrolls sideways, the status badge and every value are shown whole, and each row stays under its header

#### Scenario: Window 1440 by 1000
- **WHEN** the voucher page is open in a 1440×1000 window
- **THEN** no sideways scroll appears
