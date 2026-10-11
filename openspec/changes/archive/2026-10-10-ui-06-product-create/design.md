# Design

## Context

See `proposal.md` for motivation and scope. UI-04 already has `ProductListViewModel`, `IProductRepository`, a preview `FakeProductRepository`, and a native product detail dialog. There is no create-product/category repository contract in the client. Backend catalog and inventory folders currently provide entities and schema guidance, but repository inspection found no product/category/receipt controller or service; `shared/Requests/README.md` names request files that are not present. D28 is the visual source, but its initial-stock table and draft button have no supported contract.

## Goals / Non-Goals

**Goals:**
- Follow the existing client architecture with peer `Models`, `Views`, `ViewModels`, `Repositories`, and `Services`; keep ViewModels independent from XAML controls and HTTP.
- Reuse the product list, native dialog conventions, existing theme controls, and DI-selected preview mode.
- Provide a mock create flow that can verify validation, failure retention, success refresh, and catalog-versus-inventory boundaries.
- Keep the serialized product data shape extensible for product/variant specifications without claiming an API request DTO.

**Non-Goals:**
- Do not implement HTTP/ApiClient/JWT, server endpoint work, authoritative uniqueness checks, or server permission enforcement.
- Do not receive stock, add inventory movements, enter serial/IMEI units, or save drafts.
- Do not implement image upload/CDN storage, product edit, product detail changes, or category administration.

## Decisions

### Keep the add action in the existing product list and use a native dialog

Add a “Thêm sản phẩm” command to `ProductListViewModel` that delegates presentation to an `IProductCreateDialogService`, following the `IProductDetailDialogService` and existing single-dialog coordination pattern. The dialog is `Views/Catalog/ProductCreateDialog.xaml` with a constructor-injected `ProductCreateViewModel`; closing or cancelling returns focus to the initiating list action. The form is vertically scrollable and adapts its columns to available dialog width, using the D31 dialog guidance rather than fixed screenshot dimensions.

Alternative considered: create a separate route/page. D28 is a modal form and keeping the existing list context makes cancellation and post-create refresh straightforward.

### Use typed form models and fake repositories selected by DI

Add client-side models for the draft, category option, specification entry, and variant draft under `Models/Catalog`. Keep `IProductRepository` as the product boundary and add create behavior to it; add `ICategoryRepository` for category choices, with interfaces adjacent to fake implementations under `Repositories/Catalog`. `ProductCreateViewModel` receives those interfaces by constructor injection and coordinates state with CommunityToolkit.Mvvm commands/properties. No viewmodel references `ContentDialog`, XAML controls, or HTTP.

Use an in-memory fake repository instance shared with the list so a successful preview create is visible after refresh. Register it only in the existing explicit preview mode; unavailable implementations remain selected outside preview mode. The fake can return controlled validation/conflict/failure outcomes for UI testing, and all such results stay labeled as preview data.

Alternative considered: create a second independent fake catalog store or silently use the existing static form samples. A shared repository gives list refresh a single source of preview truth and explicit error modes avoid a hidden data-source fallback.

### Map D28 fields without inheriting unsupported actions

The form covers product name, brand, category, product SKU/barcode, serial-tracking flag, base/cost prices, image URL with load/fallback preview, key/value product specifications, and editable variant rows with identity, SKU/barcode, price, and optional variant specs. The product-level base price supplies the fallback price; variant prices are independent values. Use `decimal` for money. Do not hard-code EAN/Luhn validation without a product contract; perform format and completeness checks supported by the UI, while treating uniqueness and authoritative constraints as server-owned for later integration.

No editable opening quantity or “Lưu nháp” action is included. After preview product creation, show that inventory receipt is a separate workflow and is not available until its receiving contract/UI is implemented. In particular, serial-tracked stock requires a later receipt flow that associates each unit with a serial/IMEI; a quantity cannot stand in for those devices. Do not navigate to a placeholder and describe it as a completed receipt.

Alternative considered: retain D28's opening-stock rows and ignore them in the fake. That would imply a stock mutation while creating only a catalog record, so those controls are omitted instead.

### Keep UI validation useful but non-authoritative

Validate required identity/category values, nonnegative well-formed prices, at least one complete variant where required by the form, duplicate specification keys within a draft, and complete variant fields. Keep user input after repository rejection. Fake duplicate SKU/barcode outcomes may exercise field feedback, but the UI message must say that only the future server integration can establish global uniqueness. Do not invent a local uniqueness registry as proof of uniqueness.

### Refresh list only after successful preview creation

The dialog service returns a typed result containing success/cancel and, on success, the created preview product identifier. The list calls its existing search path with current criteria after success; cancel/failure leaves criteria and products untouched. Do not change the product detail behavior.

Alternative considered: refresh unconditionally when the dialog closes. This would create a misleading list update after cancellation or a failed submit.

## Risks / Trade-offs

- [The fake catalog may be mistaken for persisted product data] → Keep preview provenance visible in the list, dialog, and success feedback; never switch to a network source on fake failure.
- [D28 suggests initial stock can be entered with catalog details] → Omit stock mutation and draft persistence, document inventory receiving as a blocker/dependency, and require a future contract with a Serial/IMEI intake path for tracked goods.
- [Catalog request/response and category contracts are not implemented] → Keep this change entirely mock-backed; a later integration change must define category and product contracts, validation/conflict payloads, permissions, cancellation, and list refresh behavior before adding HTTP code.
- [Dialog complexity can make narrow windows awkward] → Use vertical scrolling/reflow, keyboard/focus validation, a native runtime smoke test, and the separate owner manual-acceptance task before marking visual acceptance complete.

## Migration Plan

No data migration or deployment change is required. Register the new fake repositories and ViewModel/dialog services only for preview mode; keep non-preview registrations unavailable until a separate API integration change is approved and implemented. To roll back, remove the add entry point and its dialog registrations without changing backend data.

## Open Questions

The supported backend shape for product/category create, variant persistence, duplicate-conflict errors, category availability, and inventory receiving remains unresolved. These are explicit integration dependencies and must be answered in a separate contract/API change; they do not block the preview-only UI tasks in this change.
