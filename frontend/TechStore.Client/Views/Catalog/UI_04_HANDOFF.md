# UI-04 Product List handoff

**Change:** `ui-04-product-list`  
**Updated:** 2026-10-10  
**Status:** UI-04 implementation, automated checks, and owner manual acceptance are complete. The task checklist in `openspec/changes/ui-04-product-list/tasks.md` is the source of completion status. Do not treat mock values as live catalog or inventory data.

## Implemented

- Typed Catalog models describe product/variant values, stock status, filters, sorting, zero-based paging and price-range metadata.
- `FakeProductRepository` applies combined case-insensitive name/SKU, category, brand, starting-price and stock filters, deterministic sorting and eight-item pages. The starting price is the lowest supplied variant price.
- Debug preview uses `TECHSTORE_AUTH_MODE=preview` and `TECHSTORE_CATALOG_PREVIEW` (`Populated`, `Empty`, `Delayed`, `Error`, or `MissingImage`). Unknown or missing scenario values select `Populated`. The populated fixture contains local category illustrations plus deliberately missing and failed image URLs.
- `ProductListViewModel` provides combined query state, a 300 ms search debounce, first-page reset on criteria changes, request-generation protection against stale success/error results, paging, loading/empty/error state, and retry with the same criteria/source.
- Each card updates selected variant SKU, price, availability, color label and status independently. Native XAML presents visible filters, a wrapping product grid, product image fallback, localized stock labels and page controls. `RouteId.Catalog` maps to `ProductListPage`.
- HTTP product integration remains out of scope; the page uses the explicit fake repository in preview and the unavailable repository otherwise.

## Automated verification

Executed from the repository root on Linux, 2026-10-10:

```bash
dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --logger 'console;verbosity=minimal'
```

Result: **59 passed, 0 failed**. This includes executable model, repository, preview-scenario, ViewModel race/debounce/paging/retry, and per-card variant tests.

```bash
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore
```

Result: **Build succeeded, 0 warnings, 0 errors**. The Windows target was not built on this Linux host.

## Runtime and owner acceptance

The Debug desktop app was started with the populated preview scenario using:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATALOG_PREVIEW=Populated \
  dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-build
```

The process opened and remained running. Uno logged RemoteControl/HotDesign dev-server connection errors and X11 window-manager warnings; these did not terminate the app. The project owner confirmed on **2026-10-10** that the requested manual checks passed: login → Sản phẩm, normal and narrower usable windows, vertical scroll/card reflow, keyboard/focus, search/filter combinations, sorting and page boundaries, variant selection, image fallback, and the configured populated/delayed/empty/error/retry/missing-image scenarios. No unmet behavior was reported. Exact dimensions, scale adjustment and screenshots were not required.

To launch another fixture, close the current app and replace `Populated` with `Empty`, `Delayed`, `Error`, or `MissingImage`. Login in preview mode with `admin` / `Admin@123`, then select **Sản phẩm**. The corresponding launch command is also documented in `Repositories/Catalog/README.md`.

## API dependency

No product-list HTTP endpoint or shared client DTO is established. A separate integration change must agree on the real list request/response, total-count and paging semantics, filter/sort support, category/brand option source, variant price semantics, image delivery, sellable quantity and low-stock policy, warehouse scope, authorization, and error/cancellation behavior. UI-04 does not invent an endpoint or modify backend behavior.

## Owner acceptance

Accepted by the project owner on **2026-10-10**. All requested manual checks passed; no unmet behavior was reported. Exact dimensions, scale adjustment and screenshots were not required.
