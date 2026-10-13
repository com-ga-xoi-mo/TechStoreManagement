# client-ui-foundation Specification

## Purpose

Provides a consistent, inspectable visual foundation for TechStore's Uno desktop screens before navigation and business features are implemented.

## Requirements

### Requirement: Shared TechStore visual language
The client SHALL provide reusable theme resources for the D31 colors, typography, spacing, and surface treatment so later screens can render consistently.

#### Scenario: Theme preview uses the reference palette
- **WHEN** a user opens the UI foundation preview
- **THEN** primary actions use `#2563EB`, the page canvas uses `#F8FAFC`, primary text uses `#0F172A`, and semantic success, warning, and danger examples use the D31 palette

#### Scenario: Vietnamese text at the user's display settings
- **WHEN** the preview is displayed with Vietnamese text at the user's current desktop scaling
- **THEN** headings, body text, labels, and captions remain readable without clipping or overlapping

### Requirement: Representative interactive states
The preview SHALL show representative native button, input, card, and status badge treatments with distinguishable interactive and feedback states.

#### Scenario: Button and input states
- **WHEN** a user inspects or interacts with the preview controls
- **THEN** the primary, secondary, destructive, disabled, focus, and input-error states are visually distinguishable, and validation text remains readable

#### Scenario: Pointer and keyboard interaction
- **WHEN** a user hovers, presses, disables, or keyboard-focuses a representative control
- **THEN** the rendered control, not just a separately displayed palette sample, displays a distinguishable state without losing a visible focus indicator or its validation message

#### Scenario: Status badges
- **WHEN** a user inspects success, warning, danger, and neutral badge examples
- **THEN** each badge includes readable text as well as color, a light-tint background, a visible border, and the D31 rectangular rounded treatment

#### Scenario: Badge text contrast
- **WHEN** any status badge example is rendered on its tinted background
- **THEN** its normal-size text has a measured contrast ratio of at least 4.5:1 against that background

### Requirement: Common feedback examples
The preview SHALL demonstrate loading, empty, error, and success feedback patterns without claiming that business data was loaded or saved.

#### Scenario: Inspect feedback
- **WHEN** the preview is opened
- **THEN** loading, empty, error, and success examples are each visibly labeled or locally selectable, with clear explanatory text and an actionable retry example for the error state

#### Scenario: Local retry
- **WHEN** the user activates the preview's retry action
- **THEN** only the demonstration state changes; the client does not request business data or report that a transaction was saved

### Requirement: Presentation currency format
The client SHALL provide a reusable presentation formatter for Vietnamese đồng amounts without changing the numeric value held by the caller.

#### Scenario: Whole đồng amount
- **WHEN** the formatter receives `34990000`
- **THEN** it displays `34.990.000 ₫`

#### Scenario: Non-numeric or unavailable amount
- **WHEN** the formatter receives an unavailable value
- **THEN** the preview displays an explicit placeholder rather than a misleading amount

### Requirement: Preview remains within the current application
The theme preview SHALL be inspectable from the existing desktop client without requiring authentication, API access, database access, or a new business navigation flow.

#### Scenario: Open existing application
- **WHEN** the client starts before UI-01 is implemented
- **THEN** the preview can be inspected while the existing `MainPage` navigation shell and menu remain functional

#### Scenario: Compact desktop window
- **WHEN** the user narrows the application window from its normal desktop size
- **THEN** the preview remains usable through wrapping or scrolling and its swatches, button row, badges, inputs, and feedback content are not cut off horizontally within the available area beside the navigation pane

#### Scenario: Compact feedback and input layout
- **WHEN** the user inspects the preview in a narrower usable desktop window at their current display scaling
- **THEN** both input examples and all four feedback examples remain individually readable and reachable without horizontal clipping
