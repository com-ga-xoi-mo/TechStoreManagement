# Proposal

## Why

The client still starts in the UI-00 preview hosted by a seven-item template `MainPage`; selecting a menu item does not navigate to a real destination. UI-01 establishes the persistent D01-style shell before login and business screens are added, so later screens can attach without rebuilding navigation.

## What Changes

- Replace the starter navigation surface with a native Uno/WinUI Shell containing the five primary destinations from D01: Tổng quan, Sản phẩm, Danh mục và Kho, Đơn hàng, Khách hàng. Keep the brand, selected-item treatment, content region, settings footer, and a session-aware user footer; collapse the pane at narrower usable desktop widths.
- Add the smallest DI-backed navigation, dialog, and current-session seams needed by the Shell. Route changes preserve the Shell, support sensible Back behavior, and show explicit “chưa triển khai” placeholders until each destination's own UI change is applied.
- Keep Cài đặt visible but disabled or explicitly marked unavailable; do not fabricate its screen. The footer must not present the D01 sample “Admin TechStore” as a real signed-in identity.
- Demonstrate capability-driven menu visibility with an injected preview/test session snapshot. Real capability claims and authenticated identity require a confirmed server contract and UI-02; client filtering is only a usability layer, never authorization.
- **BREAKING (starter navigation only):** remove the template's extra POS and Identity sidebar entries. POS entry points belong to later order creation; neither a POS screen nor an identity screen is implemented here.

## Capabilities

### New Capabilities

- `client-navigation-shell`: Persistent client navigation frame, route selection/back behavior, placeholder destinations, responsive sidebar/footer, and session-aware menu presentation.

### Modified Capabilities

None. The `client-ui-foundation` theme preview contract explicitly describes behavior before UI-01 and remains unchanged.

## Impact

Planning targets `frontend/TechStore.Client/` Shell Views/ViewModels, minimal Services/Navigation, Services/Dialog, Services/Session interfaces and implementations, and app DI/bootstrap. Reuse UI-00 resources and keep its preview accessible for development without making it a sixth business destination. No business Model/Repository/ApiClient, backend endpoint, DTO, database change, or target-screen implementation is included. Real login/session/capability integration and business routes remain separate follow-up work.
