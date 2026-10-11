# Design

## Context

UI-01 provides the five-destination Shell and UI-03 maps Dashboard through `UnoFrameAdapter`. The Sản phẩm route still resolves to the shared placeholder. The client has no Catalog model, repository, API client, filter contract, or product assets; the backend has Catalog entities but no product controller/endpoint or shared product DTO. UI-00 theme resources and the existing explicit-preview DI pattern are available.

The D23 reference shows a heading and total count, name/SKU search, brand/price/stock filters, category chips, sorting, product cards with images/variant swatches/price/stock, and paging. Its table toggle, add button, and card action controls are outside this change's functional scope.

## Goals / Non-Goals

**Goals:**

- Deliver D23's product-browsing flow in native Uno XAML using the existing Shell, theme, MVVM, and DI conventions.
- Make query semantics deterministic and testable through an explicitly selected fake repository, including cancellation, paging, and loading/error states.
- Keep product data clearly identified as synthetic preview content until a separate API integration has a real contract.

**Non-Goals:**

- Product table mode, create/edit/detail/delete screens, product action menus, inventory operations, image upload, or backend/API changes.
- Establishing authoritative category, brand, price, stock-threshold, availability, or permission policy.
- Adding a new UI framework or third-party grid/chart package.

## Decisions

1. **Use a dedicated `client-product-list` capability and keep the existing Shell contract.** The Shell already promises to show a route's page or placeholder; mapping Catalog to its implemented page fulfills that contract. The new capability owns the product list's visible behavior without duplicating Shell requirements.

2. **Keep the repository boundary and use mock-only data for this change.** Add `Models/Catalog`, `Repositories/Catalog`, `ViewModels/Catalog`, and `Views/Catalog` at the existing peer-directory level. `IProductRepository` sits beside `FakeProductRepository`; the ViewModel receives the interface by constructor injection. Register the fake only in the existing explicit Debug preview mode and use an unavailable implementation outside preview, matching the current Auth/Analytics pattern. Do not add HTTP, JWT, endpoint assumptions, or silent fake fallback after an error.

3. **Use one query object and one page result.** Query criteria carry search, category, brand, price band, stock state, sort, zero-based page index, and page size. The repository returns items and the filtered total from the same query. Use eight items per page to match D23. Search compares case-insensitively against name and SKU; filters combine with AND. Price bands are `< 10,000,000`, `10,000,000–25,000,000` inclusive, and `> 25,000,000` VND. A filter/search/sort change resets page index to zero. “Newest” uses the mock creation timestamp; ties use a stable product key so pages are deterministic.

4. **Keep inventory status source-provided.** The list displays the mock's available quantity and status (available, low, out of stock, discontinued). It does not calculate a low-stock threshold in the View or ViewModel. A real service must define whether quantity is on-hand or sellable, reservation treatment, thresholds, and warehouse scope before integration.

5. **Treat variant swatches as card-level preview selection.** When variants are present, the card exposes accessible swatch labels; selecting one updates only that card's selected variant, price, and availability from its supplied preview values. It does not navigate to a detail view or mutate catalog data. Starting price is the minimum supplied variant price when no variant is selected.

6. **Make asynchronous queries latest-wins.** Debounce free-text input by 300 ms, cancel the previous query on new criteria or page exit, and use a monotonically increasing request version as a second guard when a repository ignores cancellation. Clear or visibly de-emphasize old results while new criteria are loading so they cannot be mistaken for the new result. Retry reuses the active criteria and keeps the same repository selected.

7. **Use native Uno XAML layout and existing theme tokens.** Use a native, wrapping item layout that is verified on the pinned `net10.0-desktop` target; target four columns when space permits, then reflow at narrower usable widths. Keep category chips horizontally scrollable where needed and the page vertically scrollable. Use a neutral local fallback for missing/broken images; the UI source's remote image URLs are not a runtime dependency. No new package is planned.

8. **Provide test fixtures through configuration, not an in-app demo panel.** A Debug-only `TECHSTORE_CATALOG_PREVIEW` setting may select populated, empty, delayed, error, or missing-image preview data when combined with `TECHSTORE_AUTH_MODE=preview`. Do not expose a user-facing fixture switcher; production/non-preview uses the unavailable repository.

9. **Leave unsupported actions out of the active experience.** Do not offer a working list/table switch, add/edit/detail/delete, or overflow menu until their own changes exist. If a visual affordance is retained for D23 continuity, it must be clearly unavailable and cannot navigate to fabricated content.

10. **Treat the API integration as a separate dependency.** Before a real repository exists, backend/API owners must define list request/response, count and page semantics, filter and sort support, category/brand option source, variant prices, product image policy, inventory availability and low-stock threshold, warehouse scope, authorization, and error/cancellation behavior. UI-04 must not create these contracts itself.

## Risks / Trade-offs

- [Synthetic records can look authoritative] → Show a persistent preview label and document that fixture values and filters do not establish business policy.
- [Remote reference images fail or are unavailable offline] → Do not depend on the reference URLs; ensure every card has a local neutral fallback and test the missing-image fixture.
- [Uno item-layout behavior differs across targets] → Verify wrapping, scrolling, keyboard reachability, and card sizing in the desktop runtime; build the Windows target only on an available Windows host and report when unavailable.
- [The real API may use different filter/page semantics] → Keep provisional semantics in the fake repository and require a separate contract review before HTTP integration.
- [Rapid searches can publish out-of-order results] → Combine cancellation with request-version checks and add a controlled delayed-response headless test.
