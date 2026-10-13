# Tasks

> Checklist rule: mark a task complete only after its stated implementation and verification both pass. If evidence is missing or uncertain, leave it unchecked. Owner visual/interaction acceptance stays unchecked until the project owner confirms it.

## 1. Catalog detail model and repository

- [x] 1.1 Add typed `ProductDetail` and detail-variant models for product identity, image, warranty metadata, product/variant specifications, selling price, stock/status, and optional average-cost/margin values; verify decimal, nullable fields, typed specifications, and preview provenance with executable model tests.
- [x] 1.2 Extend `IProductRepository` with a cancellation-aware detail lookup and implement it in the fake repository using the same product and variant IDs as the UI-04 list; test found/not-found details, only-real-variant combinations, and data consistency against the existing summary fixture.
- [x] 1.3 Keep `UnavailableProductRepository` unavailable outside explicit preview and add deterministic missing/error/cancellation cases without silent mock fallback; verify those outcomes with repository tests.

## 2. Detail ViewModel and cost capability behavior

- [x] 2.1 Implement `ProductDetailViewModel` load, populated, unavailable, error, retry, and cancellation state through `IProductRepository`; verify retry retains the same product and controlled out-of-order requests cannot publish stale detail.
- [x] 2.2 Implement valid variant selection so SKU, price, quantity/status, variant specifications, and financial values update as one tuple; verify invalid combinations cannot be selected and one-variant products need no meaningless selector.
- [x] 2.3 Gate cost and margin properties with `ICurrentSession.HasCapability("catalog:cost-read")`, clear/suppress values without the capability, and never calculate missing margin client-side; add headless tests for authorized, unauthorized, and authorized-but-omitted-cost cases, including no sensitive accessibility-bound values in the denied state.
- [x] 2.4 Add explicit preview-only authorized and unauthorized session fixtures for manual acceptance and document how to select them; verify the granted fixture carries `catalog:cost-read` and the denied fixture does not, without describing either as real RBAC.

## 3. Native showroom dialog and Catalog entry point

- [x] 3.1 Build the native Uno `ProductDetailDialog` using D27's information hierarchy and existing D31 resources, with image fallback and distinct loading/populated/unavailable/error/retry states; verify the XAML compiles and no edit, discontinue, inventory, or Serial/IMEI action is exposed.
- [x] 3.2 Add an accessible product-card activation bound to the list ViewModel and open the detail dialog through the existing dialog boundary; verify the chosen product ID is passed correctly and list search, filters, sort, page, and selection remain unchanged after closing.
- [x] 3.3 Support valid storage/color choices, keyboard Tab/Enter/Space/Escape, visible focus, focus return to the initiating card, one active modal, and narrow-window vertical scrolling without horizontal clipping; verify through desktop runtime smoke checks and targeted UI-level tests where available.

## 4. Verification, handoff, and owner acceptance

- [x] 4.1 Link new Catalog detail Model/ViewModel sources into `tests/Frontend.UnitTests` as needed and run `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj`; build `frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`, then run explicit preview through list-to-detail, variant changes, authorized/unauthorized cost visibility, loading/error/retry, unavailable data, and close/re-entry. Record actual commands/results; build the Windows target only on Windows, otherwise mark it unverified.
- [x] 4.2 **Owner manual acceptance gate:** only after 4.1 passes, ask the project owner to test the dialog from the product list in the normal desktop window and a narrower usable window. Check product/spec text, valid variant updates, price/stock, authorized and denied cost visibility, loading/error/retry, close/Escape, keyboard/focus return, scroll, and absence of unfinished edit/IMEI actions. Record dated confirmation and unmet behavior; no exact dimensions, scale adjustment, or screenshot required. Owner confirmed all checks passed on 2026-10-10; no unmet behavior reported.
- [x] 4.3 Update a Catalog detail handoff with task scope, preview credentials/scenarios, current API and authorization dependencies, actual build/test/runtime results, Windows-target status, and dated owner acceptance; verify it does not claim mock data/permissions are authoritative or imply UI-07/UI-12 are complete.
