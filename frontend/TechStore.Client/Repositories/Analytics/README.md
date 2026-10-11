# Dashboard analytics preview data

`FakeAnalyticsRepository` is registered through DI for UI-02's explicit Debug preview. It creates one synthetic order dataset and one current inventory snapshot so the four Dashboard cards, revenue points, low-stock rows and recent orders remain internally consistent. None of these values are backend data or verified permissions.

## Preview fixtures

Run the client from the repository root:

```bash
TECHSTORE_AUTH_MODE=preview TECHSTORE_DASHBOARD_SCENARIO=populated dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile 'TechStore.Client (Desktop)'
```

In a Debug build with explicit preview enabled, `TECHSTORE_DASHBOARD_SCENARIO` accepts `populated`, `empty`, `delayed`, `error`, or `onepoint`. Restart the app to switch the fixture. Outside Debug preview, the setting is ignored and the repository selection does not enable mock mode. No fixture selector is shown in the app.

- `onepoint` returns one completed order and one dated revenue point, so the chart's sparse-data rendering can be checked directly.

## Provisional fixture definitions

- Revenue is the sum of completed synthetic orders in the selected interval; pending orders are excluded. Order count uses the same completed orders. The most recent five rows are a display subset and do not define the KPI.
- Today covers the local calendar day, last seven days includes today and six previous local days, and this month covers the full local calendar month. Every range uses `[start, endExclusive)` in `Asia/Ho_Chi_Minh`.
- Comparison is the immediately preceding interval with the same duration. A zero previous value has no percentage comparison.
- Product count is the distinct active synthetic products. A low-stock record has available quantity (`quantity - reserved`) less than or equal to its configured alert threshold. Product and low-stock counts are current at `GeneratedAt`, independent of the selected sales range.
- The fixture has no return/refund records. The real Analytics API must define settlement time/status, refunds, warehouse scope, thresholds, date boundaries/timezone, comparison and authorization before any UI value is treated as authoritative.
