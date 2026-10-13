# UI-05 Product detail handoff

**Change:** `ui-05-product-detail`  
**Updated:** 2026-10-10  
**Status:** Implementation, automated verification, and owner manual acceptance are complete. Do not treat preview values or capabilities as real catalog data or server authorization.

## Implemented scope

- Typed product-detail and valid-variant models share the UI-04 Catalog product and variant IDs. The fake repository exposes populated, delayed, error, and unavailable detail scenarios independently from the product-list scenario.
- `ProductDetailViewModel` owns loading, populated, unavailable, error/retry and cancellation state; rejects invalid variants; updates price, stock, status, specifications and optional financial values from one selected variant.
- `catalog:cost-read` controls whether cost and estimated-margin values remain in bindable state. The client never computes missing margin. Preview access can be selected with `TECHSTORE_COST_PREVIEW_ACCESS`; it is not security or evidence of real RBAC.
- Native Uno `ProductDetailDialog` presents identity, image fallback, warranty/model metadata, specs, valid variant selection, price, availability and gated cost/margin. It has loading, populated, not-found, error/retry states, narrow-window scrolling, keyboard close, one-modal coordination and focus restoration.
- Product cards expose an accessible **Xem chi tiết** action through the list ViewModel and dialog service. Closing keeps the same list context. Edit, discontinue, inventory and Serial/IMEI actions are absent.
- `UI_05_PREVIEW_GUIDE.md` documents preview selection. Use it for authorized/denied access and populated/delayed/error/unavailable detail cases.

## Automated verification

Executed on Linux, 2026-10-10:

```bash
dotnet test tests/Frontend.UnitTests/Frontend.UnitTests.csproj --logger 'console;verbosity=minimal'
```

Result: **80 passed, 0 failed**. Coverage includes repository/list identity, actual variants, unavailable/error/delay, ViewModel race/retry/cancel, selection, authorized/denied/omitted costs, preview fixtures, and detail-command product ID/list-state preservation.

```bash
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-restore
```

Result: **Build succeeded, 0 warnings, 0 errors**. The Windows target was not built because this environment is Linux.

The explicit populated preview was launched with:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_CATALOG_PREVIEW=Populated \
TECHSTORE_PRODUCT_DETAIL_PREVIEW=Populated TECHSTORE_COST_PREVIEW_ACCESS=denied \
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --no-build
```

The desktop process opened and remained running. Uno logged RemoteControl/HotDesign dev-server and X11 window-manager warnings; they did not stop startup. The owner then manually confirmed on **2026-10-10** that the requested flows passed, including opening/closing and re-entry, valid variant updates, both cost-access fixtures, delayed/error/retry/unavailable detail states, keyboard/focus, scrolling at a narrower usable window, and absence of unfinished actions. No unmet behavior was reported.

## API and authorization dependencies

No real product-detail endpoint or client response contract is established. A future integration must agree on product/variant identity, product/variant dynamic-spec shape, warranty/model fields, image delivery/fallback, selling-price semantics, available-stock meaning and warehouse scope, detail-not-found and error behavior, and request cancellation. Cost and margin require server-side authorization for `catalog:cost-read`; unauthorized responses must omit protected values. No HTTP client, endpoint, JWT, or backend behavior is added here.

UI-07 product editing and UI-12 Serial/IMEI remain out of scope and are not represented as complete.

## Owner acceptance

Accepted by the project owner on **2026-10-10**. All requested checks passed; no unmet behavior was reported. Exact dimensions, scale adjustment and screenshots were not required.
