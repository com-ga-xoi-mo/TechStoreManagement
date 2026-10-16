# Tasks

## 1. Setup & C# Markup Foundation Verification

- [x] 1.1 Verify Uno C# Markup package references and project configuration in `frontend/TechStore.Client/TechStore.Client.csproj` and `frontend/Directory.Packages.props`, ensuring `UnoFeatures` includes `CSharpMarkup` and solution builds cleanly (`dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`).
- [x] 1.2 Inspect and confirm all required Uno C# Markup namespace extensions (`using Uno.Extensions.Markup;`) and syntax helpers for WinUI controls are available.

## 2. Refactor LoginPage to Declarative C# Markup

- [x] 2.1 Refactor `frontend/TechStore.Client/Views/Identity/LoginPage.cs` to fluent C# Markup layout and bindings:
  - Declare branding panel and login form card using fluent layout hierarchy (`Grid`, `StackPanel`, `Border`).
  - Replace manual `PropertyChanged` and input event handlers with fluent declarative bindings for username (`TextBox`), password/reveal, remember me checkbox, error banner, and loading progress ring with submit button command.
  - Verify that `LoginPage` compiles without errors and tests pass via `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`.

## 3. Refactor SettingsPage to Declarative C# Markup

- [x] 3.1 Refactor `frontend/TechStore.Client/Views/Settings/SettingsPage.cs` to fluent C# Markup layout and bindings:
  - Compose settings view structure fluently using `Uno.Extensions.Markup` combined with CommunityToolkit `SettingsExpander` and `SettingsCard`.
  - Bind store configuration fields (name, address, hotline, email, tax id) two-way declaratively to `SettingsViewModel`.
  - Bind theme selection and save button state / success banner without manual `PropertyChanged` string matching.
  - Verify `SettingsPage` compiles without errors and passes headless unit tests.

## 4. Refactor ShellPage to Declarative C# Markup

- [x] 4.1 Refactor `frontend/TechStore.Client/Views/Shell/ShellPage.cs` to fluent C# Markup layout and bindings:
  - Structure `NavigationView`, header, nav items, content host (`PageHost`), and pane footer using fluent markup chains.
  - Bind current page title, navigation error notifications, and user session profile (display name, avatar initials, role description) declaratively to `ShellViewModel`.
  - Ensure lifecycle hooks (`Loaded`, `Unloaded`, `SizeChanged`), `ShellNavigationAdapter`, and `ContentDialogPresenter` attachment remain intact.
  - Verify `ShellPage` compiles cleanly and passes all navigation/shell tests in `tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`.

## 5. Verification & Clean-up

- [x] 5.1 Run full solution build (`dotnet build TechStore.sln`) and test suite (`dotnet test TechStore.sln`) to ensure zero regression across frontend and backend projects.
- [x] 5.2 Confirm all legacy imperative object initialization and manual `PropertyChanged` boilerplate in `LoginPage.cs`, `SettingsPage.cs`, and `ShellPage.cs` have been eliminated and conform to repository guidelines.
