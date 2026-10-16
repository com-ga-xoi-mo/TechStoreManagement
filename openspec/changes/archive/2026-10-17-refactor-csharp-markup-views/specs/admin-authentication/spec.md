# Spec Delta

## ADDED Requirements

### Requirement: Admin Login View Declarative C# Markup
The desktop client SHALL implement the administrator and staff login screen (`LoginPage.cs`) using Uno Platform C# Markup Fluent API declarations (`Uno.Extensions.Markup`). All UI components, including the branding panel, credential input fields (`TextBox`, `PasswordBox`), submit button with progress ring, error notification banners, and password visibility toggle, SHALL declare layout and data bindings fluently without imperative event subscriptions or manual `PropertyChanged` event wiring.

#### Scenario: User enters credentials and initiates login via declarative binding
- **WHEN** the user types username and password and triggers the login action
- **THEN** the inputs are synchronized via declarative two-way bindings to `LoginViewModel.Username` and `Password`, and the submit button triggers `LoginCommand` with the busy indicator bound to `IsLoggingIn`

#### Scenario: Display authentication error banner via declarative binding
- **WHEN** the view model emits an error state (`HasError = true`, `ErrorMessage`)
- **THEN** the error banner automatically becomes visible and displays the localized error message without imperative code-behind event handling
