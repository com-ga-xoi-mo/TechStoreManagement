# Design

## Context

The backend already exposes Admin-protected customer search, profile, create/update, point-history, and order-history endpoints. Its current soft-deactivation operation is shaped as `DELETE /api/customers/{id}` and there is no reactivation operation, so the API contract must be revised to express the reversible activity lifecycle without a delete route. The client currently registers an empty `CustomerListViewModel` and displays a XAML placeholder; Catalog shows the established repository-to-presentation-model pattern, while the shell provides page activation lifecycle and a serialized modal service. Uno C# Markup is enabled. The supplied customer screens are in `mockup_stitch/stitch_techstore_retail_admin_dashboard/`.

The customer list needs total spending and last purchase date in addition to the existing summary; order count remains available in the API/profile but is omitted from the list table to keep the primary action visible without horizontal scrolling. The detail mockup also includes order history and a purchased-devices/IMEI view. The existing customer API does not yet return all of these fields. It already provides paginated loyalty point events, which can power a read-only transaction-history view.

## Goals / Non-Goals

**Goals:**
- Match the supplied Stitch customer list, create/edit dialogs, detail experience, and confirmation flow using C# Markup and existing design tokens.
- Provide customer list aggregates and paginated order, purchased-device/IMEI, and loyalty transaction history through Admin-protected APIs.
- Keep ViewModels headless-testable and follow the active page/dialog lifetime and cancellation patterns.
- Preserve customer and historical data when changing customer activity state, and allow accidental deactivation to be reversed.
- Show active customers by default while allowing Admins to view inactive customers or all customers explicitly.

**Non-Goals:**
- Calculate, accrue, redeem, reverse, or otherwise mutate loyalty points in this feature.
- Permanently delete a customer or migrate historical customer/order data.
- Change order pricing, inventory reservation, fulfillment, or completion behavior.
- Convert other contributors' XAML pages or remove XAML outside the customer feature.
- Add UI dependencies or invent layouts inconsistent with the supplied Stitch screens.

## Decisions

1. **Use a feature client repository and presentation models.** The repository wraps `IApiClient`, maps shared DTOs into customer-facing models, preserves paging metadata, and translates API validation/conflict failures into forms the ViewModels can present. ViewModels do not issue HTTP calls or reference UI controls.

2. **Extend the customer API with list aggregates and purchased-device history.** Customer search returns order count, total spending, and last completed purchase date. Order count includes all the customer's orders; total spending sums only `Completed` orders; last purchase is the latest completion time, and is empty when no order is completed. A new paginated read-only customer purchased-device query returns product/variant, order reference/time, and linked serial/IMEI for completed order items. These are customer-facing read queries; they do not alter order lifecycle or loyalty data. The existing customer order-history endpoint remains paginated and includes pending/cancelled orders as historical records.

3. **Use one request per visible page, not one request per customer/order.** The customer list query returns the required aggregates in its page response. Order history, purchased devices, and loyalty events load from their respective paginated customer endpoints when the detail dialog opens or its tab is selected. No N+1 per-row or per-order fan-out is used.

4. **Keep loyalty information read-only.** Display the persisted point balance in the customer summary and show paginated persisted transactions in a dedicated history view, including point change, balance after, reason, timestamp, and related order when present. The UI offers no point mutation or calculation action and does not define a conversion formula.

5. **Keep the Customer route and replace only its legacy page.** `CustomerListPage` remains the route target and `IRoutePage` implementation, rebuilt as a C# Markup `Page`. Customer create/edit and detail experiences use C# Markup dialogs hosted by the existing shell dialog root and receive the selected customer ID as needed. Existing route registration and shell navigation remain intact.

6. **Use replaceable async requests for search and activation-scoped work.** Search/filter requests cancel or supersede prior requests; page teardown prevents stale results from committing. History tabs load paginated data and expose empty/error/retry states. This follows `PageViewModel` lifecycle guarantees.

7. **Model customer removal as reversible activity changes, not deletion.** Replace the delete-shaped API with idempotent `PATCH /api/customers/{id}/deactivate` and `PATCH /api/customers/{id}/activate` operations. Both return 204 when the customer is already in the requested state and 404 for an unknown customer. Expose no `DELETE /api/customers/{id}` route and no customer repository/service remove operation. Preserve the Stitch confirmation pattern for deactivation, label it as deactivation, and explain that the profile and history are retained. Reactivation is available for inactive customers.

8. **Use the supplied Stitch screens and current C# design tokens as visual acceptance criteria.** Use `AppColors`, `AppTypography`, and shared controls where suitable; bind theme colors on elements so light/dark switching updates live. Compare the completed customer list/dialogs against the supplied reference. Do not add a UI package solely for visual effects.

9. **Keep the customer table usable at the target window width.** Center initials within circular avatars, place compact eye and pencil actions together beneath the centered action header for viewing and editing, omit the order-count table column, and expose deactivation from the detail view. Keep header and item-grid widths synchronized with the viewport so the actions remain reachable without horizontal scrolling at the target width.

10. **Preserve the MVVM boundary in the customer views.** Resolve page and dialog ViewModels through UI dependency injection, connect editable state and commands with typed C# Markup bindings, and keep direct repository calls inside ViewModels via `ICustomerRepository`. The View may own control creation, focus, and modal open/close lifecycle, but it does not construct feature ViewModels or retrieve customer data itself.

11. **Keep feature dialogs as dedicated C# Markup views.** `CustomerListPage` owns list presentation and coordinates dialog lifetime/focus only. Create/edit and customer-detail modal contents belong to separate customer View classes, receive their ViewModels through DI, bind state and commands, and do not render customer data imperatively in response to ViewModel property-change events.

12. **Make date of birth quick to enter in both profile forms.** The shared create/edit form accepts `dd/MM/yyyy` and offers a date-picker flyout through a black calendar button placed on the left side of the field. The ViewModel owns parsing, validation, and the selected date; the view connects both controls with typed bindings and does not synchronize them through control events. The API contract remains `DateOnly?`.

13. **Keep database foreign keys unchanged in this feature.** Soft deactivation/reactivation updates `customers.is_active` and does not invoke referential delete behavior. The existing order relationship uses `SET NULL` and the loyalty-point relationship uses `CASCADE`, so the absence of an application hard-delete path is enforced with API surface, layering, and HTTP tests. Changing those foreign keys to `RESTRICT` is deferred as optional schema hardening for a future coordinated migration; it is not an implementation task or prerequisite for this change.

14. **Keep newest-created sorting implicit until alternatives exist.** Customer search already orders by `CreatedAt` descending with ID tie-breaking. Remove the one-option `Mới nhất` selector instead of presenting a control that cannot change behavior. A future sort feature must add explicit request semantics and at least one meaningful alternative before restoring the selector.

15. **Commit search text on each edit and debounce API requests.** The search field uses a two-way binding with property-change source updates. `CustomerListViewModel` retains the short replaceable debounce and cancellation behavior so typing starts filtering without Enter while obsolete responses cannot overwrite newer text.

16. **Use one tier presentation mapping.** Customer list badges, list avatars, detail badges, and detail avatars share the same theme-resource palette: information blue for Standard, neutral silver for Silver, warning gold for Gold, and accent purple for Platinum. Header/value alignment is defined per column and reused by every row; filter controls share size, radius, and spacing above the table.

17. **Return the primary product category with customer order history.** Extend `OrderSummaryDto` with `PrimaryCategoryName`, load the existing `OrderItem.Product.Category` relationship in the repository query, and map it through the client repository into `CustomerOrder`. This is a read-contract extension only and requires no entity, foreign-key, or migration change.

18. **Use product category and persisted status for detail presentation.** Extend purchased-item read results with the existing product category name so the C# Markup detail view can choose a phone, laptop, or accessory icon without guessing from product text. Keep order, warranty, and device-status tone selection in presentation models; the View binds theme-resource variants. A non-serial purchase has no per-unit warranty or inventory-device status, so its `Không quản lý` label is an explanatory UI state rather than a new database status.

19. **Center profile placeholders and show the date hint plainly.** Create and edit dialogs use vertically centered text content at the existing field height. The visible hint is `dd/mm/yyyy`; .NET parsing and formatting retain `dd/MM/yyyy` internally because uppercase `MM` means month and lowercase `mm` means minutes.

## Risks / Trade-offs

- [List aggregates and device history extend the API contract] → Specify paging, aggregate semantics, and history fields before implementation; cover query results with backend tests and client mappings with frontend tests.
- [Search can issue excessive requests while typing] → Debounce and cancel/ignore stale requests; preserve a visible search action if debounce proves unreliable.
- [Historical device rows can include non-completed orders if not constrained] → Include only completed order items in purchased-device history; retain all order statuses in the separate order-history view.
- [Loyalty display can be confused with accrual ownership] → Show persisted balance and ledger only; do not calculate or mutate points.
- [Stitch depicts permanent deletion] → Keep the visual confirmation pattern but clearly present the approved soft-deactivation behavior.
- [A later contributor could reintroduce hard deletion] → Keep remove methods out of the customer service/repository and add an HTTP contract test proving `DELETE /api/customers/{id}` is unavailable.
- [Inactive customers can disappear after a successful action] → Default the list to active customers and provide explicit inactive/all activity filters plus a reactivation action.
- [Typing can still appear Enter-dependent when the binding updates only on focus loss] → Set the search binding source update trigger to `PropertyChanged`, retain ViewModel debounce, and cover it with headless behavior plus runtime acceptance.
- [Tier colors can drift between list and detail] → Resolve badge and avatar colors through one shared tier presentation mapping backed by existing theme resources.
- [Category display could cause an order-history N+1 query] → Include product/category in the existing paged order repository query and map the loaded relationship once per page.

## Migration Plan

No data migration is needed. Keep the existing `is_active` column and foreign-key behavior, revise the customer activity endpoints, extend the customer read API and DTOs, register frontend customer services, replace the customer XAML page with its C# Markup implementation, run backend/frontend tests and supported target builds, then verify the API-backed workflow against a running backend. Rollback consists of reverting this feature change; no database schema changes are introduced.

## Open Questions

None. Aggregate semantics, reversible soft deactivation, and deferral of optional foreign-key hardening are settled for this feature.
