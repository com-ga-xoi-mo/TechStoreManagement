# Proposal

## Why

The Uno client currently has only a starter `MainPage` and default WinUI resources. The first TechStore screens need a shared visual baseline so their colors, typography, controls, feedback states, and money formatting stay consistent with design D31 and dashboard reference D01.

## What Changes

- Add a minimal set of native Uno/WinUI XAML resources for TechStore colors, typography, spacing, and the button, input, card, and badge examples needed to establish the theme.
- Add a small preview surface that demonstrates these resources, Vietnamese text, focus/disabled/error states, and loading/empty/error feedback without building a business screen.
- Verify readable badge text, complete feedback examples, actual hover/pressed/disabled/focus behavior, and usable layout in the user's normal and a narrower desktop window. For UI-00, the project owner accepts direct visual inspection and a recorded confirmation instead of exact window/scale measurements or mandatory screenshots; a successful build or a checked task alone is not visual acceptance.
- Add a presentation-only currency formatter and a documented icon/image fallback approach for later screens.
- Preserve the current `MainPage` navigation structure and startup behavior; Shell migration belongs to UI-01.

## Capabilities

### New Capabilities

- `client-ui-foundation`: The desktop client can render a consistent TechStore visual foundation and inspect representative states before business screens are built.

### Modified Capabilities

None. Existing OpenSpec capabilities cover database schema, migrations, and seeding.

## Impact

The future implementation is limited to `frontend/TechStore.Client/` XAML resources, small presentation helpers, and an accessible preview. It may require a bundled Inter font only if a suitable local asset and license are available; otherwise the design specifies a system-font fallback. No API, repository, DTO, backend, or database change is needed for UI-00.
