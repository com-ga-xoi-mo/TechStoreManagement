# Tasks

## 1. Extend customer read API

- [x] 1.1 Add customer search aggregates to the response: total order count across all statuses, completed-order spending total, and latest completed timestamp; add backend unit tests for mixed statuses, no completed orders, and stable customer paging.
- [x] 1.2 Add a paginated customer purchased-items query/DTO returning completed-order product/variant, order reference/time, and linked serial/IMEI when present; test empty pages, null identifiers, ordering, pagination, and unknown customer behavior.
- [x] 1.3 Verify all customer endpoints remain Admin-protected and the new read queries do not mutate order, inventory, or loyalty records; run the targeted backend unit test project.
- [x] 1.4 Replace `DELETE /api/customers/{id}` with idempotent Admin-only `PATCH /api/customers/{id}/deactivate` and `PATCH /api/customers/{id}/activate`; keep remove operations out of the customer service/repository and test 204/404 behavior, preserved history, authorization, repeat calls, and 405 for DELETE. Do not change foreign keys or create a migration.

## 2. Customer API client and presentation models

- [x] 2.1 Add customer list/profile/history presentation models and a customer repository for search, detail, point-history, order-history, purchased-items, create, update, and deactivate endpoints; verify DTO mappings, query encoding, paging metadata, 404, validation, conflict, and no-body success with frontend repository tests.
- [x] 2.2 Add customer repository headless tests using fake `IApiClient` responses for malformed payloads, API failures, cancellation, and all request bodies; verify the targeted frontend unit test project passes.
- [x] 2.3 Update the customer client repository to use the two PATCH activity endpoints and expose no hard-delete operation; cover both no-body success responses, failures, and cancellation with frontend repository tests.

## 3. Customer list and profile workflows

- [x] 3.1 Implement customer list ViewModel search, tier filter, paging, aggregate display data, create entry point, deactivation confirmation state, activation cancellation, loading/empty/error/retry behavior; verify frontend tests cover debounce/stale responses and all states.
- [x] 3.2 Replace the legacy CustomerListPage XAML and code-behind with a pure C# Markup page matching the Stitch list, using existing tokens and controls; verify the customer route resolves and the frontend builds for `net10.0-desktop`.
- [x] 3.3 Implement and register the C# Markup create/edit customer dialog with server field-error display and retained form values; verify ViewModel tests cover valid create/update, field validation, duplicate-phone conflict, cancellation, and refresh behavior.
- [x] 3.4 Let the shared create/edit form accept `dd/MM/yyyy` or a date-picker selection from the black calendar button on the field's left; keep parsing and selected-date state in the ViewModel.
- [x] 3.5 Default the customer list to active customers and add `Active`, `Inactive`, and `All` activity filters that compose with search, tier, paging, cancellation, empty/error states, and list refresh; cover the query behavior with headless ViewModel tests.

## 4. Customer detail and histories

- [x] 4.1 Implement the customer detail ViewModel and modal for profile, read-only points/tier/activity, paginated point transactions, paginated order history, and paginated purchased devices/serial/IMEI; verify headless tests cover mapping, empty states, failures, paging, and prove no loyalty write operation is issued.
- [x] 4.2 Extract customer create/edit and detail modal visual trees into dedicated C# Markup View classes; keep `CustomerListPage` limited to list UI and modal lifetime/focus coordination. Use DI-resolved ViewModels, typed bindings/commands, bound history item templates, and no property-change/event-driven UI synchronization; verify frontend tests/build and keyboard behavior for opening, closing, and returning focus.
- [x] 4.3 Adapt the Stitch delete confirmation to an explicit “Vô hiệu hóa” confirmation that preserves the customer and historical data; test confirmation, cancellation, success, and API failure behavior.
- [x] 4.4 Show deactivation only for active profiles and `Kích hoạt lại` only for inactive profiles, expose no permanent-delete action, allow inactive profiles to remain readable/editable, and test reactivation success/failure plus activity-specific action visibility.

## 5. Integration and acceptance

- [x] 5.1 Remove only the replaced customer XAML artifacts, register required customer services, and verify no stale type/resource references remain with `rg` plus complete frontend builds for Windows and desktop targets.
- [x] 5.2 Run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`, `dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`, and `dotnet build TechStore.sln`; resolve all failures before marking the feature complete.
- [x] 5.3 Resolve runtime customer UI findings: remove the one-option newest sort selector while retaining newest-created API order; make typed search update through the existing debounce without Enter; apply a shared tier palette to list/detail badges and avatars; align headers with row values; separate and round the tier/activity filters; center the order-status column; and return/display the primary product category beneath the order-history product. Add focused backend/frontend tests and rebuild the client without changing the database.
- [x] 5.4 Run the app against the local API and verify Admin can search/filter active, inactive, and all customers; see accurate aggregates; create/edit active or inactive profiles; inspect persisted point and order histories plus purchased devices/IMEI; deactivate and reactivate safely; and never receive a permanent-delete action. Verify live typed search, fixed newest-created order without a redundant selector, tier-matched badges/avatars, aligned columns, separated rounded filters, primary product categories, category icons, completed/cancelled order colors, valid/expired/untracked warranty and device colors, centered form placeholders, and the visible `dd/mm/yyyy` date hint. Compare the finished screens with the supplied Stitch design and record the tested target and any visual differences.

## Verification notes — 2026-10-19 modal command regression

- Reproduced the create-button failure in the running Windows client before the fix. View-owned modal commands were incorrectly resolved through the ViewModel/item data context. Assign those stable commands directly, bind row command parameters to the current item, and give form/detail/confirmation contents their respective ViewModel data contexts. Business state remains bound; no click/property-change synchronization was introduced.
- Verified the rebuilt `net10.0-windows10.0.26100` client against the local API using Windows UI Automation: create opens; empty submit shows required-name/phone errors; cancel closes; eye action opens the selected customer's populated details; points/devices tabs switch; edit opens prefilled fields; deactivation opens confirmation and cancellation closes it; customer-name action opens the correct profile; closing details returns focus to search.
- These checks did not save a customer or confirm deactivation. Successful persistence, complete keyboard acceptance, and full Stitch visual acceptance remain part of task 5.4. Task 4.2 remains open because detail-view extraction is still incomplete.

## Verification notes — 2026-10-19 date-of-birth entry

- The shared customer create/edit form now binds a `dd/MM/yyyy` input and a date-picker flyout opened by the black calendar button on the field's left to `CustomerFormViewModel`; parsing and selected-date synchronization remain outside the View. The earlier desktop build and strict OpenSpec validation preceded this final visual adjustment. The picker has not yet been exercised in a running app; that remains part of runtime acceptance task 5.4.

## Verification notes — 2026-10-19 customer activity and detail extraction

- Extracted the detail modal visual tree to `CustomerDetailDialogView`; `CustomerListPage` now coordinates modal lifetime, loading, focus, and follow-up edit/deactivation flows. The detail view binds profile/history values and commands through the detail ViewModel, and exposes activate/deactivate according to profile state.
- Verified `net10.0-desktop` build (0 warnings/errors), `net10.0-windows10.0.26100` build (0 errors; existing MVVM Toolkit AOT and publish-profile warnings), all frontend unit tests (239/239), all backend unit tests (280/280), `git diff --check`, and `openspec validate add-customer-management-ui --strict`.
- Ran API acceptance against the local PostgreSQL database using a temporary API process on `http://localhost:5186`: Admin login succeeded; active/inactive/all filters returned the expected seeded customers; editing the inactive customer's unchanged profile succeeded; activate/deactivate and repeated calls returned 204; point, order, and purchased-device history remained unchanged; hard-delete returned 405. The verification customer was restored to its original inactive state. Temporary API/client processes were stopped afterward.
- Full UI acceptance for task 5.4 remains pending: the newly built Windows executable did not open a visible window when launched directly in this environment, so keyboard/focus behavior and visual comparison of the extracted detail modal against Stitch have not been rechecked after extraction. Recheck by launching the Windows target from Visual Studio with the API running, then complete task 5.4.

## Verification notes — 2026-10-20 customer detail presentation

- Added category-aware icons to purchased items through a read-only category field; order-history `Cancelled` now uses danger colors, and warranty/device labels use success or danger colors according to persisted dates and tracking availability. The create/edit form centers input content vertically and shows the visible date hint as `dd/mm/yyyy` while preserving correct .NET month parsing.
- Reapplied the Git-excluded local Dev 5 fixture. It now has four orders for the active test customer: current tracked warranty, untracked Sony accessory, cancelled order, and expired tracked warranty. PostgreSQL confirmed four orders, three completed purchased items, one expired device, and Sony in `Accessories`.
- Focused frontend tests passed (15/15). Desktop client, Windows client, and backend builds passed; backend and Windows build outputs used temporary directories because a running API process held the normal backend output DLL. Windows still reports existing MVVM Toolkit AOT and publish-profile warnings. Task 5.4 remains open until the user checks the rebuilt UI in Visual Studio and the complete acceptance flow is verified.
- A temporary Development API on port 5186 returned three completed purchased items with category names `Accessories`/`Smartphones`, linked serial/IMEI and both warranty dates, while the order-history response included the cancelled order. The temporary API was stopped; the user's existing API process was not interrupted.

## Verification notes — 2026-10-21 integration with `dev`

- Merged the current local `dev` (including the C# design-token foundation and Order frontend) into the Feature 5 branch. Resolved shared API transport, dependency injection, and test-double overlap without dropping either module's registrations or PATCH overloads.
- Replaced customer-list static `StitchTokens` colors with live theme-resource references from `AppColors`; the detail/form/confirmation views and tier palette now use the same token keys. Bound customer table header and row widths to the viewport-owned table width so resizing cannot leave existing rows at the old width.
- After integration, backend unit tests passed 280/280, frontend unit tests passed 482/482, the whole solution and both desktop/Windows client targets built with zero errors, and strict OpenSpec validation passed. Existing Windows AOT/publish-profile and backend EF version warnings remain; they are not Feature 5 runtime-acceptance evidence.
- Task 5.4 remains open. The user's already running client was left untouched, and no new visual/keyboard acceptance was claimed. Recheck the latest Feature 5 build against the local API in Visual Studio, including light/dark switching, responsive table alignment, the create/edit/detail/confirmation flows, and the Stitch comparison before checking this task or archiving the change.

## Verification notes — 2026-10-21 final user acceptance

- The user completed the remaining manual 5.4 acceptance in the Windows client launched from Visual Studio and confirmed the Feature 5 workflows and presentation are OK. The local fixture was used to inspect the remaining device-status badge colors. No visual differences or blocking issues were reported.
