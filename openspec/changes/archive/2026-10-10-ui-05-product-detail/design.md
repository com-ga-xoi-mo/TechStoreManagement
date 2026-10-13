# Design

## Context

UI-04 already provides `Models/Catalog`, `IProductRepository`, `FakeProductRepository`, `UnavailableProductRepository`, `ProductListViewModel`, an explicit preview login, shared D31 resources, and a Shell dialog service. It does not provide a product-detail model/query or a showroom. The existing session abstraction exposes capability keys, but the current preview credentials do not exercise the cost-read capability. D27 is a native showroom reference with identity, image, technical specifications, configuration/color options, price, average cost, estimated margin, stock, and action buttons. The edit, discontinue, and IMEI destinations are not implemented and remain excluded.

See proposal.md for the motivation and specs for observable behavior.

## Goals / Non-Goals

**Goals:**

- Reuse the Catalog list, data identity, theme resources, DI setup, session capability model, and existing dialog presentation boundary.
- Keep loading and detail state in a headless-testable `ProductDetailViewModel`; keep repository calls behind `IProductRepository`.
- Ensure every selectable configuration maps to an actual preview variant, with price, availability, and variant-specific data updated as one selection.
- Demonstrate cost visibility for preview sessions with and without `catalog:cost-read`, while making clear that preview capabilities are not authorization evidence.
- Deliver a native, keyboard-usable, responsive ContentDialog, and keep the current product list intact when it closes.

**Non-Goals:**

- Product editing, discontinuing products, inventory mutation, Serial/IMEI browsing, new Shell destinations, HTTP/API integration, JWT changes, or backend authorization implementation.
- Deriving official selling price, average cost, profit, availability, or permissions in the View or ViewModel.
- Recreating every button in D27 when its destination does not exist.

## Decisions

1. **Add a focused detail capability and a small list delta.** `client-product-detail` owns showroom behavior. `client-product-list` gains only the accessible activation that opens the showroom. This preserves the durable split between browsing and inspection instead of expanding the list capability into editing or inventory management.

2. **Extend the existing Catalog repository boundary.** Add a typed detail result and `GetProductDetailAsync(productId, cancellationToken)` to `IProductRepository`; implement it in the existing fake and unavailable repositories. Reuse product and variant IDs from `FakeProductRepository` so the detail dialog cannot show an unrelated product. Keep product-level and variant-level technical specifications as typed key/value data; keep monetary values as `decimal`. Do not create a second Catalog repository or HTTP adapter.

3. **Treat each variant as a valid selectable tuple.** The detail model carries the real available variant combinations and their IDs, labels, SKU, selling price, available quantity/status, optional variant specifications, and optional financial values. The ViewModel selects one tuple; the UI may visually group its storage/color labels, but it must disable or omit combinations absent from the model. It does not cross-join independent option lists or invent stock for a combination.

4. **Use session capability only for presentation and server-provided values.** `ProductDetailViewModel` checks `ICurrentSession.HasCapability("catalog:cost-read")`. Without it, the ViewModel clears/suppresses cost and margin properties so neither visible labels nor accessibility text reveal the values. With the capability, it still displays only values supplied by the repository; it does not calculate margin. Add deterministic preview-only credentials/capability fixtures for both authorized and unauthorized paths so both can be manually tested. Preview UI gating is not security: a real integration must enforce permission in the backend and omit unauthorized financial fields from its response.

5. **Keep the modal in the View/service boundary.** Add a narrow product-detail dialog presentation operation beside the existing `Services/Dialog` abstraction (or extend it only if that can host native custom content without leaking controls). A native XAML ContentDialog owns its XamlRoot and show/close lifecycle; `ProductDetailViewModel` receives only repository/session abstractions and the selected product ID. It must not reference `ContentDialog`, Page, Frame, or `HttpClient`. Preserve the existing one-modal-at-a-time behavior, Escape close, and return focus to the invoking product item.

6. **Open details from the existing Catalog list.** Add an accessible detail activation to each product card and bind it to an asynchronous list ViewModel command/service contract. The dialog view uses a transient detail ViewModel. Closing returns to the same list instance with search, filters, sort, page, and selection unchanged. Do not change Shell route definitions.

7. **Use the existing explicit preview selection.** The fake repository supplies deterministic detail data, including cases with and without cost values and a failed/missing image when needed. The unavailable repository stays unavailable outside preview; an HTTP error never falls back to mock data. The dialog shows a persistent preview label in mock mode. Keep loading, not-found/unavailable, error, and retry states distinct and cancellation-aware.

8. **Match D27's information hierarchy, not its unsupported actions.** Use a split detail layout on wider windows and a vertically scrolling/reflowing layout on narrow windows. Include close, product identity/status, image with fallback, specs, actual configuration choices, selected-variant selling price and stock, and permission-gated cost/margin. Omit discontinue, edit, and Serial/IMEI actions. UI-07 and UI-12 are future dependencies before those actions can be linked.

9. **Keep tests with the behavior they verify.** Add headless repository and ViewModel tests for identity consistency, missing values, valid-variant selection, cancellation/retry, capability gating, and no client-side margin calculation. Use the running Uno desktop app for ContentDialog/XamlRoot, keyboard/focus, responsive, image, and manual-owner checks; a build alone cannot verify these behaviors.

## Risks / Trade-offs

- [The backend has no established product-detail or cost-authorization response contract] → Keep this change fake-only; record required fields, warehouse scope, cost capability, stock semantics, and errors as integration dependencies instead of inventing an endpoint.
- [A local capability check can be mistaken for security] → Label both accounts and values as preview; require server-side enforcement and server omission of protected fields in a future integration.
- [D27 shows variant combinations or actions not present in data/implemented destinations] → Render only repository-supplied variants and omit edit, discontinue, and Serial/IMEI actions.
- [A ContentDialog opened from a product card can lose XamlRoot or focus] → Reuse the central dialog host, serialize modal presentation, and smoke-test open, Escape/close, return focus, and re-entry in the desktop runtime.
- [Cost/margin values could leak through hidden bindings or accessibility labels] → Clear cost properties when the capability is absent and cover the unauthorized UI state with ViewModel tests and owner manual acceptance.

## Migration Plan

No data or backend migration is needed. Add the new capability and list delta, implement through explicit preview DI, then integrate a real repository only after its contract and server-side cost authorization are approved in a separate change.
