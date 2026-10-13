# Tasks

## 1. Edit models and preview repository

- [x] 1.1 Add typed product-edit snapshot, product/variant draft, and save-result models under `Models/Catalog`; verify editable values are separate from list/showroom models and contain no stock mutation, transaction history, or product-delete command.
- [x] 1.2 Extend `IProductRepository` with edit-load and save operations; implement them in the shared fake repository and unavailable repository, register edit scenarios through explicit preview DI, and verify cancellation tokens flow through without HTTP/DTO/UI dependencies.
- [x] 1.3 Make fake edit updates atomic and shared with product-list reads; cover success, delayed load/save, unavailable product, general failure, duplicate identifier conflict, and a configured variant-history conflict. Add repository tests proving cancel/rejection never changes stored data and success does.

## 2. ViewModel, draft behavior, and validation

- [x] 2.1 Implement `ProductEditViewModel` with injected repository/category/session dependencies and CommunityToolkit.Mvvm; cover load/loading/error/unavailable states, independent row state, add/remove draft variants, first-invalid-field focus, submit/cancel, and ensure ViewModel has no XAML, ContentDialog, or HTTP dependency. Add headless tests for switching variants and cancelling without mutation.
- [x] 2.2 Add field validation for required product/variant identity, numeric nonnegative prices, and complete/nonduplicate specs; implement explicit editable SKU suggestions derived from model/variant values and verify manual values are not overwritten. Test validation, suggestion behavior, and conflict/failure retention.
- [x] 2.3 Apply preview capability signals to edit entry and cost fields; keep Serial/IMEI tracking mode read-only. Verify a session without edit capability cannot invoke the normal edit flow, cost fields are hidden without cost-write capability, and preview labels never imply real RBAC.

## 3. Native dialog and product-list integration

- [x] 3.1 Build `ProductEditDialog` using D24 as the tabbed master/detail pattern and D25/D26 for fields/SKU suggestion; include product identity, category/brand, specs, variant SKU/barcode/prices, capability-gated cost, image URL field/fallback, loading/validation/conflict/saving states, and Cancel/Save. Verify there is no product-delete action, no inventory/IMEI mutation, and no claimed image upload; explicitly test the known UI-06 image-link preview issue and record it as unresolved if it still fails. Owner manual test on 2026-10-10 confirmed the HTTP image URL still does not display; recorded for follow-up after database/media storage is available.
- [x] 3.2 Add `IProductEditDialogService` and dialog implementation using the existing modal gate and focus-restoration pattern; verify typed cancelled/failed/saved outcomes, cancellation/load cleanup, and return focus to the initiating product-card action.
- [x] 3.3 Add the accessible edit action and command to the existing product card/list; after a successful preview save reload using current search/filter/sort/page criteria, while cancel/failure/conflict leaves the current list state unchanged. Add focused ViewModel tests for these outcomes.
- [x] 3.4 Register transient edit ViewModel/dialog service and fake preview capability scenarios in DI; update the catalog handoff with UI/mock scope, changed files, API/permission/transaction/image dependencies, and the image-link test outcome. Verify non-preview mode never silently selects a fake implementation.

## 4. Integrated verification and owner acceptance

- [x] 4.1 Run relevant `dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj` tests and `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop`; launch the desktop app and smoke-test the edit dialog. Attempt Windows target build only where a compatible Windows environment is available; record unavailable checks honestly in the handoff.
- [x] 4.2 Owner visual/manual acceptance: after 4.1 passes, ask the owner to test opening and editing a product at the current and a narrower still-usable window; switch variant tabs after entering data; test add/remove draft variant, suggestions, invalid-field focus, cost-field visibility, cancel/no mutation, save/list refresh, simulated duplicate/history conflicts with retained input, keyboard/scrolling, and image-link preview. Record dated confirmation and all unmet behavior in the handoff; no exact scale or screenshot required. Owner confirmed on 2026-10-10 that the tested edit flows are OK except the HTTP image URL does not display; deferred for follow-up once a database/media-storage design is available.
