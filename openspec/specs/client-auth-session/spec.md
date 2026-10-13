# client-auth-session Specification

## Purpose

Provides a desktop sign-in experience and a clear mock-driven preview session lifecycle so the TechStore login and Shell can be reviewed without implying backend authentication exists.

## Requirements

### Requirement: Accessible login form
The client SHALL present a native login form based on the D11 two-region layout, with a usable narrow-window arrangement and no design-only state controls or prefilled credentials.

#### Scenario: Open login
- **WHEN** the client starts without a restorable verified session in normal mode
- **THEN** it shows the username and password fields, show/hide control, remember-session option, and submit action without entering the Shell

#### Scenario: Narrow desktop window
- **WHEN** the user narrows the window while the login form is open
- **THEN** the form, feedback, and submit action remain readable and reachable through reflow or scrolling without horizontal clipping

#### Scenario: Keyboard and password visibility
- **WHEN** the user navigates with Tab and Enter or toggles password visibility
- **THEN** focus is visible, the expected control acts, and toggling visibility does not change the password value

#### Scenario: Enter password in the running desktop app
- **WHEN** development preview mode is enabled and the user focuses the password field and types a password
- **THEN** the field accepts input, masks it by default, and the submitted command receives the entered value without showing or logging it as plain text

#### Scenario: No design-only controls
- **WHEN** the login page is shown
- **THEN** it contains no state-switcher buttons, sample username/password, or unverified server-ready indicator copied from D11

### Requirement: Validate and submit credentials
The client SHALL validate required fields, submit a single login attempt at a time, and display the outcome without exposing the password.

#### Scenario: Missing fields
- **WHEN** the user submits an empty or whitespace-only username or an empty password
- **THEN** field-level Vietnamese errors appear, focus moves to the first invalid field, and no repository request is sent

#### Scenario: Invalid submit gives visible feedback
- **WHEN** the user presses Đăng nhập with a missing required field while login is available
- **THEN** the button responds by showing the relevant field error, not by silently returning from validation

#### Scenario: Pending request
- **WHEN** a valid login request is in progress
- **THEN** the page shows a loading state and prevents a duplicate submit until that attempt finishes or is cancelled

#### Scenario: Wrong credentials
- **WHEN** the auth source rejects the username or password
- **THEN** the page shows a nonrevealing credentials error, keeps the username, clears the password, and remains on login

#### Scenario: Locked or inactive account
- **WHEN** the auth source explicitly reports a locked or inactive account
- **THEN** the page shows a distinct account-unavailable message without presenting a login success or inferring this outcome from an unspecified status code

#### Scenario: Network failure
- **WHEN** the auth source cannot be reached
- **THEN** the page shows a connection error with retry, preserves the username, and remains on login without silently switching data sources

### Requirement: Preview sessions remain unverified
The client SHALL gate Shell entry with the configured mock login flow and SHALL never present a mock result as server-verified identity or permission.

#### Scenario: Explicit preview login succeeds
- **WHEN** development preview mode is enabled and the configured fake source accepts a test login
- **THEN** the client enters the placeholder Shell with its session visibly labelled preview/unverified, never as a real administrator

#### Scenario: Explicit preview starts at login
- **WHEN** the client starts in development preview mode without a session
- **THEN** it displays the empty login form and does not enter the Shell until the mock login succeeds

#### Scenario: Preview mode is off without backend
- **WHEN** no real auth integration is configured and preview mode is off
- **THEN** the login clearly reports authentication unavailable, and the client does not fabricate a successful authentication or expose protected routes even if mock credentials are entered

#### Scenario: Authentication unavailable is explained
- **WHEN** submit is unavailable because normal mode has no configured real auth source
- **THEN** the login page visibly explains why the action is unavailable rather than presenting an inert button with no feedback

#### Scenario: Restart after preview
- **WHEN** the app restarts after a mock login
- **THEN** it opens login; it does not restore access merely from a cached name, role, or preview marker

### Requirement: Remember session unavailable in preview
The client SHALL not persist a password, token or mock session for “Ghi nhớ đăng nhập” while protected storage and a real refresh contract are absent.

#### Scenario: Remember unavailable
- **WHEN** the user views the login form in this UI/mock change
- **THEN** the client clearly disables or labels the option unavailable and does not write a persistent credential

#### Scenario: Restart has no saved secret
- **WHEN** the user exits after a preview login
- **THEN** no password, token or preview session secret is restored at the next start

### Requirement: Sign-out and simulated session expiry
The client SHALL end local preview access on sign-out or a simulated unrecoverable session-expiry event and return to login without leaving Shell content available through Back.

#### Scenario: Sign out
- **WHEN** the user invokes sign-out from an active session
- **THEN** the client clears local session and token state, removes protected navigation history, and returns to login

#### Scenario: Simulated unauthorized response
- **WHEN** the mock error source reports an unrecoverable 401/session-expiry outcome
- **THEN** the client clears local preview access once, returns to login, and gives an understandable expired-session message

#### Scenario: Simulated forbidden response
- **WHEN** the mock error source reports a 403 outcome
- **THEN** the client reports insufficient permission without treating the preview session as signed out solely because of that outcome

#### Scenario: Simulated responses are reachable in preview
- **WHEN** a developer invokes the UI-02 simulated 401 or 403 path through the running preview app
- **THEN** the corresponding session transition and user feedback occur without claiming a live HTTP response

#### Scenario: Preview sign-out has no server effect
- **WHEN** sign-out occurs in preview mode
- **THEN** the client removes local access without claiming a backend token was revoked
