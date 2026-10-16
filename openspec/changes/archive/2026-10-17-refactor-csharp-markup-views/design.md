# Design

## Context

`ShellPage.cs`, `SettingsPage.cs`, and `LoginPage.cs` were written using imperative WinUI/Uno object creation (`new Control()`, `Grid.SetRow()`, `Children.Add()`) and explicit C# event subscriptions (`ViewModel.PropertyChanged += ...`, control event handlers).
See `proposal.md` for motivation. The Uno Platform SDK already enables `CSharpMarkup` feature in `TechStore.Client.csproj` (`<UnoFeatures>Mvvm; SkiaRenderer; CSharpMarkup;</UnoFeatures>`), which brings in `Uno.Extensions.Markup.WinUI` code generators and extension methods for fluent element styling and data binding.

## Goals / Non-Goals

**Goals:**
- Replace imperative UI construction in `ShellPage.cs`, `SettingsPage.cs`, and `LoginPage.cs` with declarative fluent extension methods from `Uno.Extensions.Markup.WinUI`.
- Utilize declarative data bindings (`.Bind(() => vm.Property)` / `.BindCommand(() => vm.Command)`) wherever supported, eliminating manual `PropertyChanged` event wiring.
- Preserve 100% of existing visual design (Stitch tokens, layout dimensions, corner radius, paddings, responsive behavior).
- Ensure ViewModel contracts (`ShellViewModel`, `SettingsViewModel`, `LoginViewModel`) remain clean POCOs inheriting from `ObservableObject` / `PageViewModel` without UI dependencies so that headless tests (`TechStore.Client.UnitTests`) continue passing without modification.

**Non-Goals:**
- Modifying backend APIs, authentication services, or shared DTOs/requests.
- Re-architecting `ShellNavigationAdapter` or `ContentDialogPresenter`.
- Rewriting other pages (`ProductListPage`, `OrderListPage`) that may have their own assigned tasks/milestones.

## Decisions

### 1. Fluent Layout Trees via `Uno.Extensions.Markup`
- **Choice**: Structure visual trees using nested fluent declarations (e.g. `new Grid().RowDefinitions(...).Children(...)`).
- **Rationale**: Eliminates local temporary variables and imperative `Add()` statements, aligning with standard modern Uno Platform practices.
- **Alternative considered**: Keeping imperative C# objects with only binding extensions. *Rejected* because the goal is full adoption of C# Markup Fluent API as outlined in repository guidelines.

### 2. Declarative MVVM Binding vs Manual PropertyChanged
- **Choice**: Use `.Bind(() => vm.Property, mode: BindingMode.TwoWay)` for form inputs and `.Bind(() => vm.Property)` for read-only displays, plus binding converter / visibility helpers where appropriate.
- **Rationale**: Removes brittle manual string-based `PropertyName` comparisons in `PropertyChanged` event handlers, reducing boilerplate by ~60%.
- **Alternative considered**: Retaining `PropertyChanged` subscriptions. *Rejected* as it violates the core intent of C# Markup MVVM binding.

### 3. CommunityToolkit SettingsControls Integration in SettingsPage
- **Choice**: Wrap `SettingsExpander` and `SettingsCard` in C# Markup layout, binding control contents (e.g. `TextBox.Text`, `ComboBox.SelectedIndex`) fluently.
- **Rationale**: Preserves the modern WinUI 3 Settings experience while leveraging fluent declarations.

## Risks / Trade-offs

- **[Markup Extension Availability on Custom / Third-Party Controls]** → `SettingsCard` and `SettingsExpander` from CommunityToolkit may not have dedicated fluent markup extensions for every property.
  *Mitigation*: For standard properties, standard framework extension methods apply. Where extension methods are missing, assign properties via C# object initializers while using fluent wrappers for parent/child hierarchies.
- **[Two-Way Binding Latency on PasswordBox]** → `PasswordBox.Password` in WinUI is intentionally not a DependencyProperty for security reasons.
  *Mitigation*: Bind the `PasswordRevealBox` (TextBox) fluently, and for `PasswordBox`, handle password synchronization cleanly via targeted event or specialized binding helper if needed.

## Migration Plan

1. Verify build toolchain and markup extension namespace imports (`using Uno.Extensions.Markup;`).
2. Refactor `LoginPage.cs` to fluent C# Markup and verify login flow.
3. Refactor `SettingsPage.cs` to fluent C# Markup with CommunityToolkit SettingsControls.
4. Refactor `ShellPage.cs` to fluent C# Markup preserving navigation adapter and user session pane footer.
5. Run unit test suites to guarantee zero regression on ViewModels and client repositories.
