# UI-05 preview fixtures

These fixtures exist only to inspect the client presentation. They are not real users, server RBAC, or a source of authoritative product data. The backend must independently enforce authorization and decide whether cost fields are returned.

Run from the repository root in a Debug build. Close any existing client first because environment settings are read at process startup:

```bash
TECHSTORE_AUTH_MODE=preview \
TECHSTORE_CATALOG_PREVIEW=Populated \
TECHSTORE_PRODUCT_DETAIL_PREVIEW=Populated \
TECHSTORE_COST_PREVIEW_ACCESS=denied \
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

After the preview login, open **Sản phẩm** and choose a product to inspect the showroom with cost hidden. For the presentation-only cost-visible fixture, change the final setting to `authorized`:

```bash
TECHSTORE_AUTH_MODE=preview \
TECHSTORE_CATALOG_PREVIEW=Populated \
TECHSTORE_PRODUCT_DETAIL_PREVIEW=Populated \
TECHSTORE_COST_PREVIEW_ACCESS=authorized \
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

Only the exact value `authorized` enables the `catalog:cost-read` capability in the in-memory preview session; missing, `denied`, or unknown values keep it absent. `TECHSTORE_COST_PREVIEW_ACCESS` does not alter real authentication and does not work as a server permission grant.

For detail states, set `TECHSTORE_CATALOG_PREVIEW` to `Delayed`, `Error`, or `Empty`. The `Error` scenario exposes retry; the `Empty` scenario contains no list items, so use a product selected from `Populated` when verifying a missing-detail result through automated tests.

Keep the list populated while testing dialog states by changing `TECHSTORE_PRODUCT_DETAIL_PREVIEW` independently:

- `Populated`: normal detail values.
- `Delayed`: a two-second loading state before values appear.
- `Error`: an error message and retry control; retry repeats the configured error.
- `Unavailable`: a not-found state while product cards remain available in the list.

`TECHSTORE_CATALOG_PREVIEW=Populated` should remain set for those dialog checks.
