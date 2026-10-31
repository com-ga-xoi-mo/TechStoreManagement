# Design

## Context

See `proposal.md`. The Orders page uses a shared search control and existing theme tokens.

## Goals / Non-Goals

**Goals:**
- Match the supplied Orders toolbar reference.
- Scope the 40 DIP search height to the Orders page.

**Non-Goals:**
- Change the order table, order detail dialog, other pages, or order behavior.
- Change shared search-control defaults or theme behavior.

## Decisions

- Add a per-instance height setter to `SearchBoxControl` and apply it only in `OrderListPage`.
- Keep search, clear, filter, reset, and create commands bound to their existing ViewModel commands.
- Use existing design tokens for the primary action colors and typography.

## Risks / Trade-offs

- Toolbar dimensions can vary at narrow window sizes; the requested scope is the supplied desktop reference.

## Migration Plan

No migration is required.
