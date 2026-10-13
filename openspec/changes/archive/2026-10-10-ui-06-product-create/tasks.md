# Tasks

## 1. Product create models and preview repositories

- [x] 1.1 Add typed catalog create-draft, category-option, specification-entry, and variant-draft models under `Models/Catalog`; verify the models represent product-level and variant-level SKU/barcode/prices/specifications without any stock quantity or draft-persistence field.
- [x] 1.2 Extend `IProductRepository` with a create operation and add `ICategoryRepository` beside its implementation; verify both contracts use client Models and `CancellationToken`, with no HTTP, DTO, or UI-control dependency.
- [x] 1.3 Implement preview and unavailable repository behavior and register them through existing preview-mode DI; verify preview mode shares one in-memory catalog between list and create, configured failure/duplicate scenarios remain preview-only, and non-preview mode never falls back to a fake or network source.
- [x] 1.4 Add focused repository tests for category loading, successful preview insertion/list visibility, cancellation, and configured conflict/failure; verify tests explicitly do not treat mock uniqueness or persistence as server behavior.

## 2. Form state, validation, and dialog

- [x] 2.1 Implement `ProductCreateViewModel` using injected repository interfaces and CommunityToolkit.Mvvm; verify required fields, numeric money validation, specifications/variant row editing, loading, first-invalid-field information, retry preservation, and duplicate-submit prevention through headless tests.
- [x] 2.2 Add the native `ProductCreateDialog` based on D28 and existing D31 controls; verify bindings cover identity, category, prices, image URL preview/fallback, dynamic product/variant specs, variants, field errors, loading, failure, cancel, and success while omitting initial stock and “Lưu nháp”.
- [x] 2.3 Add the product-create dialog service using the existing single-dialog/focus-restoration pattern; verify cancel, close, failure, and success return distinct outcomes and restore focus to the list action.
- [x] 2.4 Add the accessible “Thêm sản phẩm” action to `ProductListViewModel`/`ProductListPage`; verify a successful result refreshes the current criteria and cancel/failure does not reset or falsely refresh the list.
- [x] 2.5 Add ViewModel tests for invalid submit, correction after field errors, fake conflict/error preserving entered data, success result, cancel with no insertion, and one active submission; verify all tests use fake repositories without requiring a UI thread.

## 3. Inventory boundary and handoff

- [x] 3.1 Add explicit form/help/success messaging that inventory receipt is a separate pending workflow; verify no create action changes inventory quantity or ledger and serial-tracked products cannot accept quantity-only stock, with the missing receipt contract/UI-09/UI-13 recorded as a dependency.
- [x] 3.2 Complete agent-side verification: run `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj`, build `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`, and launch the desktop app; record actual results and changed files in the UI-06 handoff, leaving any failed/unrun check explicitly incomplete.
- [x] 3.3 Owner visual/manual acceptance (confirmed 2026-10-10): owner reports the UI is accepted except that adding/using an image link does not work. Record image-link preview/handling as follow-up work; do not imply image upload or full image handling is complete. Other create-flow behaviors were accepted on the owner's manual test; no exact scale or screenshot was required.
