# Catalog Preview Repository

`FakeProductRepository` provides offline data for the UI-04 product list and UI-06 product-create preview. It is registered only when the app is explicitly started in preview mode (`TECHSTORE_AUTH_MODE=preview`). It does not represent live catalog, inventory, authorization, or an implemented backend endpoint.

`FakeCategoryRepository` supplies preview-only category choices. Outside preview mode, `UnavailableProductRepository` and `UnavailableCategoryRepository` fail explicitly; there is no HTTP fallback. Product create preview behavior can be selected with `TECHSTORE_PRODUCT_CREATE_PREVIEW=Success|DuplicateSku|DuplicateBarcode|Error|Delayed`. Conflict checks and inserted in-memory products exist only to exercise the UI and are not server uniqueness or persistence guarantees.

The create preview adds products to the same in-memory repository instance used by the list. It never changes stock or records an inventory movement. Stock receipt (including per-device Serial/IMEI intake for tracked products) and draft persistence require separate implemented contracts and screens.

## Select a preview scenario

Set `TECHSTORE_CATALOG_PREVIEW` before launching the desktop app. Scenario names are case-insensitive; an absent or unknown value selects `Populated`. No in-app scenario panel is added.

| Scenario | Result |
| --- | --- |
| `Populated` | Synthetic catalog with multiple pages, local category illustrations, and missing/failed-image fixtures. |
| `Empty` | Empty result for the current criteria. |
| `Delayed` | Populated result delayed by two seconds; cancellation is honored. |
| `Error` | Repository throws a controlled preview exception so the UI error/retry state can be checked. |
| `MissingImage` | One product whose image URL is null, for fallback testing. |

Example from the repository root on Linux:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATALOG_PREVIEW=Empty \
  dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

Replace `Empty` with `Populated`, `Delayed`, `Error`, or `MissingImage` to select another fixture. The fake source is selected through DI; no HTTP request is made.

## Synthetic data notes

- The populated scenario contains 23 products, including an iPhone 15 Pro Max, a missing-image fixture, and a deliberately failed-image fixture.
- The default page size is eight products; the dataset spans three pages.
- Records include Apple, Samsung, Asus, Dell, and Xiaomi, four categories, and all four stock states.
- Product starting price is the lowest priced variant and is used for the price-band filter and price sorting.
- Variant availability is synthetic and does not indicate actual store stock.
