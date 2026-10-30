# Design

## Context

The affected pages use both C# Markup and legacy WinUI controls. Shared colors and typography are available through `AppColors`, `AppTypography`, `AppMetrics`, and `AppControlStyles`; the Orders page also uses `SearchBoxControl`. See `proposal.md` and the `frontend-design-system` delta for the visible outcomes.

## Goals / Non-Goals

**Goals:**
- Reuse existing theme tokens and shared control behavior for the presentation adjustments.
- Keep the Orders-specific change inside the heading and filter toolbar.
- Keep search height configurable per page so unrelated consumers retain their current dimensions.

**Non-Goals:**
- Change order processing, data loading, the order table, or the order detail modal.
- Import or merge another contributor's feature branch as part of the visual adjustment.
- Change APIs, persistence, or business rules.

## Decisions

- Apply the existing semantic theme brushes directly to changed controls and use existing typography and metric tokens where available. This keeps the UI aligned with the current Light/Dark resource model.
- Keep the search control's default dimensions unchanged and set the 40 DIP height only where the Products and Orders references require it. A global height change would alter unrelated screens.
- Map Serial/IMEI lifecycle values to existing `StatusTone` categories in a small presentation helper, keeping status labels and business state in the ViewModel unchanged.
- Preserve the Orders table and modal from the current `dev` baseline. The screenshot scope covers only the heading and filters, so no order-row or detail-dialog changes are needed.

## Risks / Trade-offs

- Small layouts may wrap headings or filter controls differently. Keep existing responsive containers and verify the page after build.
- Inventory status colors could be mistaken for business state if color is read alone. Keep the textual status visible beside each semantic color.

## Migration Plan

No data or deployment migration is required. Build the desktop target and review the affected pages in the running client. Revert only the presentation changes if a visual regression is found.
