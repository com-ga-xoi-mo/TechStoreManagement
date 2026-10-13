# client-navigation-shell Specification

## Purpose

Provides a persistent, usable navigation frame for TechStore's desktop client so later business screens can attach to stable routes without replacing the shell or implying unfinished features are available.

## Requirements

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

### Requirement: Route selection and history
The client SHALL keep route content, selected menu state, and Back behavior consistent while retaining the same shell instance.

#### Scenario: Select a destination
- **WHEN** the user selects any available primary destination
- **THEN** the shell remains visible, exactly that destination is active, and the content region shows its matching page or an explicit “chưa triển khai” placeholder

#### Scenario: Re-select current destination
- **WHEN** the user selects the already active destination
- **THEN** the shell does not add a duplicate history entry or lose the current content

#### Scenario: Back through destinations
- **WHEN** the user has visited more than one destination and invokes Back
- **THEN** the content returns to the previous destination and the active menu item follows it

#### Scenario: Back at shell root
- **WHEN** the user invokes Back with no earlier destination
- **THEN** the shell and current content remain visible without an empty page or navigation out of the application

#### Scenario: Unsupported destination
- **WHEN** a route cannot be resolved or opened
- **THEN** the client keeps the shell visible and shows a readable unavailable/error message without falsely marking that route active

### Requirement: Placeholder boundaries
The client SHALL label destinations that have no implemented business page and SHALL not represent placeholder content as live business data.

#### Scenario: Unimplemented destination
- **WHEN** the user opens Sản phẩm, Đơn hàng, Khách hàng, or the Tồn kho or Serial / IMEI tab before its screen is implemented
- **THEN** the destination identifies the unavailable feature and does not show invented business data or actions

#### Scenario: Inventory category destination
- **WHEN** the user opens Danh mục & Kho after the category-tree screen is installed
- **THEN** the Shell remains visible and its content shows the category/inventory tab workspace with the category tree selected by default

#### Scenario: Settings unavailable
- **WHEN** the user inspects or activates Cài đặt before a settings design and route exist
- **THEN** it remains visibly unavailable and does not open a fabricated settings page

### Requirement: Responsive and keyboard-usable shell
The client SHALL keep navigation and content reachable in normal and narrower usable desktop windows at the user's display settings.

#### Scenario: Narrow window
- **WHEN** the user narrows the window while viewing any destination
- **THEN** the navigation pane collapses or becomes otherwise compact, the active destination remains identifiable, and navigation/content remain reachable without horizontal clipping

#### Scenario: Keyboard navigation
- **WHEN** the user navigates the shell with Tab, Enter, Escape, or the platform Back action
- **THEN** focus remains visible, available destinations can be activated, the compact pane can be dismissed, and Back follows route history

#### Scenario: Vietnamese labels
- **WHEN** the shell is displayed at the user's desktop scaling
- **THEN** the five Vietnamese labels, footer status, and placeholder text remain readable without overlap

### Requirement: Session-aware menu presentation
The client SHALL distinguish an explicit UI preview session from an authenticated session and SHALL treat client-side capability filtering only as presentation behavior.

#### Scenario: Explicit preview session
- **WHEN** the shell runs before login integration in a configured preview mode
- **THEN** all five placeholder destinations can be exercised and the footer clearly identifies the session as preview/unverified rather than a signed-in administrator

#### Scenario: Capability snapshot supplied
- **WHEN** the client is given a session snapshot with a known allowed-destination set
- **THEN** the menu shows only allowed destinations, disallowed routes cannot be opened through navigation, and a lost active destination moves to an allowed fallback without leaving stale selection

#### Scenario: Capability unavailable outside preview
- **WHEN** no trusted capability snapshot is available and preview mode is off
- **THEN** protected destinations are not exposed as authorized, and the shell does not infer permissions from the D01 sample role or a locally chosen label

#### Scenario: Server remains authoritative
- **WHEN** a later business route makes a protected API request
- **THEN** UI visibility does not replace backend authorization, and the client does not claim access solely because a menu item was visible

#### Scenario: Mock login enters Shell
- **WHEN** a configured development mock login succeeds
- **THEN** the Shell opens with its session clearly labelled preview/unverified and only the preview-available destinations

#### Scenario: Preview does not bypass login at startup
- **WHEN** the client is started in development preview mode without a session
- **THEN** it shows LoginPage first and requires the mock login command before opening the Shell

#### Scenario: Session ends
- **WHEN** the user signs out or the preview session is ended by a simulated expiry
- **THEN** the Shell is removed, its route history is cleared, and Back cannot reopen protected content
