# Design

## Context

See `proposal.md` for the change motivation and scope. UI-05 supplies the product showroom/detail vocabulary; UI-06 supplies the product list, shared fake repository, category repository, create models, dialog coordinator, and a known unresolved image-URL preview issue. `IProductRepository` currently supports search, detail, and create only; there is no edit model, save operation, or product-edit dialog. No catalog edit API/contract was found. D24 is the primary visual reference, D25 supplies the fuller product form, and D26 shows an editable SKU suggestion. The D24 artwork also shows product deletion, which belongs to UI-16 and is excluded here.

## Goals / Non-Goals

**Goals:**
- Add a mock-backed edit flow that shares the existing in-memory preview catalog with the list.
- Keep a typed, isolated edit draft until a successful repository save; preserve each variant's unsaved state across D24 tabs.
- Exercise validation, authorization presentation, conflict/failure retention, accessible operation, and current-list refresh.
- Make SKU suggestions explicitly advisory and keep server-owned uniqueness, transaction rules, and authorization out of the client.

**Non-Goals:**
- No HTTP repository, ApiClient, endpoint, JWT, backend, or database changes.
- No product-delete action, inventory/ledger mutation, Serial/IMEI intake, or unsupported tracking-mode change.
- No image file picker, upload, or CDN integration. The existing image-link preview issue is documented; do not claim it is fixed by reusing the URL field.
- No category or brand administration and no generic refactor of the UI-06 create form.

## Decisions

### Use an edit-specific snapshot and draft behind the existing product repository

Add typed product-edit data, draft, variant-draft, and result models alongside the existing catalog models. Extend `IProductRepository` with a get-for-edit operation and an update operation that accept a `CancellationToken`; keep the fake and unavailable implementations beside the interface. The edit snapshot includes editable product identity/category/brand/image/specifications and variant SKU/barcode/price/specifications, plus read-only stock and Serial/IMEI mode metadata. Cost values are optional and only populated for an explicit preview capability that allows cost editing.

`ProductDetail` is a read-oriented showroom projection and omits some editable values such as product-level identifiers and category IDs, so the edit operation returns an explicit edit snapshot rather than overloading the showroom model. No DTO/API contract is implied.

Alternative considered: use `ProductCreateDraft` and `ProductDetail` directly. That conflates create and update semantics, loses edit-only metadata, and risks treating display-only or missing fields as editable input.

### Keep mutable edit state separate from the catalog record

The ViewModel copies the loaded edit snapshot into observable product fields and one row ViewModel per variant. Selecting a variant tab changes only the active row; it never recreates or reloads row values. Add/remove operations affect the local collection only. Cancel, Escape, close, load failure, or save rejection does not mutate the repository's stored product. The fake repository replaces the catalog record atomically only after an accepted save result.

Alternative considered: bind TextBoxes to the shared `ProductSummary` or repository objects. That would make cancel unreliable and allow partially edited list cards to leak before save.

### Open the editor from the product card and coordinate dialogs through DI

Add a separate accessible “Chỉnh sửa” action to the existing product card. It resolves `IProductEditDialogService`, loads selected product edit data into a transient `ProductEditViewModel`, presents a native `ProductEditDialog`, and restores focus to the opener when closed. Reuse the shared dialog gate, product list, styles, common data models, and category repository; do not modify the showroom's behavior or create a second product route.

On successful save, return a typed dialog outcome and reload the existing list query using the current criteria. On cancel/failure/conflict, leave the list's result set and query state unchanged. Product delete is not rendered in this dialog.

Alternative considered: place the edit action inside the UI-05 showroom. D24 presents the edit surface over the catalog list, and keeping the action on the existing product card avoids broadening the showroom change.

### Keep suggestions and capability gates explicitly non-authoritative

The SKU suggestion is produced only on explicit user request, derived from the current model and variant labels, remains editable, and is not refreshed over a manual value when unrelated fields/tabs change. It is a convenience string, never evidence of uniqueness or acceptance. Fake responses may simulate duplicate identifier conflicts and a variant-history conflict; the UI preserves the draft and reports the repository result without asserting that this is the server's final rule.

Use preview-only capability signals to show the edit affordance and cost edit fields. The fake session may provide `catalog:product-edit` and `catalog:cost-write` for testing. These labels are not API authorization claims. The client does not change Serial/IMEI tracking mode in this flow; the eventual API and backend remain authoritative for every permission, transaction constraint, and cost rule.

Alternative considered: infer edit or cost rights from a role name or the existing read-cost capability. That would expose sensitive fields based on an assumption rather than an explicit capability.

### Carry the known image-link defect as an explicit limitation

The edit form may retain and edit the product image URL using the current shared preview pattern, but this change does not add local-file upload or CDN handling. UI-06's handoff records that adding/using an image link was not working during owner acceptance. Treat working URL preview/load feedback as a verification item; if the shared converter still fails, record that unmet behavior clearly and do not mark image handling fixed. A dedicated image implementation can follow separately if required.

Alternative considered: silently treat an entered URL as a successfully displayed image. That would repeat the known UI-06 acceptance gap.

## Risks / Trade-offs

- [A fake save may be mistaken for persisted catalog data] → Label the dialog/results as preview and never fall back from a failed fake operation to HTTP.
- [Cost fields can leak sensitive data in a later real integration] → Gate the preview field by an explicit capability and require API-side authorization before any real data is returned or changed.
- [Changing/removing a variant with transaction history may be restricted] → Use explicit fake conflict scenarios, preserve the draft, and leave the exact rule to the future catalog contract/backend.
- [D26 suggestions may be mistaken for valid/unique SKUs] → Make suggestions opt-in, editable, and label them advisory; server validation remains authoritative.
- [Image URL preview is already known to be incomplete] → Identify it in the handoff and owner acceptance; do not count it as solved by the editor.

## Migration Plan

No server deployment or data migration is needed. Register the edit dialog/ViewModel and fake edit behavior only in the existing explicit preview path; keep unavailable behavior outside preview. Rollback consists of removing the product-card edit action, dialog registration, and fake update operation without changing persisted data.

## Open Questions

Real integration still needs edit snapshot/update contracts, concurrency/version semantics, identifier conflict payloads, category/brand update rules, variant transaction restrictions, write-cost permission mapping, and a supported image URL/upload strategy. These do not block mock UI work but must be resolved before HTTP integration.
