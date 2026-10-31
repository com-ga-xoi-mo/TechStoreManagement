# Design

## Context

The Customers list already uses labeled previous/next buttons around a page label. Inventory pages currently render icon-only buttons and a button for each page. Their ViewModels already expose navigation commands and the available-direction state.

## Goals / Non-Goals

**Goals:**
- Reuse existing navigation commands and disabled-state properties.
- Display one concise page label between the two text buttons on all three inventory tabs.

**Non-Goals:**
- Change pagination calculations, page size, filtering, or API requests.
- Change the Customers pager or other module footers.

## Decisions

- Replace numbered page buttons with “Trang trước” and “Trang sau”, matching Customers. This creates a predictable compact footer and removes the inventory-only numbered-button treatment.
- Add a computed page label to each affected ViewModel and notify it when current page or page count changes. This keeps formatting out of the view and ensures the text updates after loading, filtering, and navigation.
- Keep the existing enablement rules for previous and next so boundary states remain correct.

## Risks / Trade-offs

- [Risk] A footer may become wider because the controls use text labels → Reuse the existing responsive footer container and verify the desktop build.

## Migration Plan

No data migration is needed. Build the desktop client, open the three inventory tabs, and verify the page label updates while navigating.
