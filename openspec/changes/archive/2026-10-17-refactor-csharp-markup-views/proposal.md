# Proposal

## Why

Currently, `ShellPage.cs`, `SettingsPage.cs`, and `LoginPage.cs` are implemented using imperative C# object initializers (`new Grid()`, `new Border()`, manual `Children.Add()`) with verbose manual `PropertyChanged` and control event wiring, rather than declarative Uno C# Markup fluent DSL (`Uno.Extensions.Markup.WinUI`). This violates the project's architectural guidelines for modern frontend C# Markup, introduces high boilerplate, and creates maintenance overhead.

Refactoring these views to standard Uno C# Markup Fluent API while preserving all existing view model contracts, design tokens (`StitchTokens`), and accessibility labels ensures UI consistency and leverages type-safe markup declarative bindings.

## What Changes

- **Convert ShellPage to Fluent C# Markup**: Refactor `frontend/TechStore.Client/Views/Shell/ShellPage.cs` to use declarative `Uno.Extensions.Markup` fluent extension methods (e.g., layout chains, declarative properties, event/command bindings) while keeping its routing adapter integration, theme synchronization, and session display logic intact.
- **Convert SettingsPage to Fluent C# Markup**: Refactor `frontend/TechStore.Client/Views/Settings/SettingsPage.cs` to declarative C# Markup fluent layout combining Uno C# Markup with CommunityToolkit SettingsControls (`SettingsExpander`, `SettingsCard`), declarative two-way bindings for store settings form fields and theme selector, and automated error/success banner visibility bindings.
- **Convert LoginPage to Fluent C# Markup**: Refactor `frontend/TechStore.Client/Views/Identity/LoginPage.cs` to declarative C# Markup fluent syntax, replacing manual `PropertyChanged` and event handlers with fluent markup bindings (`.Bind(...)`, `.Command(...)`) for authentication form inputs, loading indicator, error banner, and password reveal toggle.
- **Verify Package & Markup Namespaces**: Ensure `UnoFeatures` includes `CSharpMarkup` and required usings (`Uno.Extensions.Markup`) are properly configured in `TechStore.Client`.

## Capabilities

### New Capabilities
<!-- Capabilities being introduced. -->

### Modified Capabilities
- `frontend-shell-navigation`: Refine the view implementation contract for `ShellPage.cs` and `SettingsPage.cs` to specify pure Uno C# Markup Fluent API with declarative MVVM bindings instead of imperative object construction and manual event-driven synchronization.
- `admin-authentication`: Ensure the desktop UI specification for the administrator login view (`LoginPage.cs`) mandates Uno C# Markup Fluent API declarative binding patterns for credentials, busy/loading states, and validation banners.

## Impact

- **Affected Code**:
  - `frontend/TechStore.Client/Views/Shell/ShellPage.cs`
  - `frontend/TechStore.Client/Views/Settings/SettingsPage.cs`
  - `frontend/TechStore.Client/Views/Identity/LoginPage.cs`
- **Dependencies**: Uno Platform C# Markup feature (`CSharpMarkup` in `TechStore.Client.csproj` via Uno.Sdk).
- **APIs**: No backend API or shared contract changes. ViewModels (`ShellViewModel`, `SettingsViewModel`, `LoginViewModel`) remain untouched to ensure 100% headless test compatibility.
