# Spec Delta

## MODIFIED Requirements

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
