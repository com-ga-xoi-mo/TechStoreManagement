# Design

## Context

See [proposal.md](proposal.md) for motivation and [client-ui-foundation/spec.md](specs/client-ui-foundation/spec.md) for observable requirements. The client targets Uno 6.7 on `net10.0-desktop` and Windows. An initial UI-00 implementation already adds `Styles/TechStoreTheme.xaml`, a preview, a converter, and a preview ViewModel inside the starter `MainPage` navigation shell. The remaining work is to verify and, where necessary, correct interactive states and compact layouts, then record runtime evidence. The D31 HTML contains one pill-shaped badge CSS rule that conflicts with its own visible board and text, so the image/board's 4 px rectangular badge wins.

## Goals / Non-Goals

**Goals:**

- Establish a compact palette and keyed styles that later views can reuse without copying hex values.
- Render a small, accessible preview inside the current startup page to inspect text and control states at the user's available window size and display settings.
- Provide a presentation formatter for VND and document icon/image fallbacks.

**Non-Goals:**

- Rebuild Shell navigation, login, dashboard, or D31's full specification board.
- Add API calls, repositories, fake business data, status enum mappings, or a complete control library. Minimal DI registration for the preview ViewModel is permitted; no business services are registered.
- Guarantee every future screen's contrast or layout; each later screen must check its own use of these resources.

## Decisions

### 1. Resources stay native and explicitly keyed

Keep `Styles/TechStoreTheme.xaml` merged in `App.xaml` after `XamlControlsResources`. Define named colors/brushes for primary `#2563EB`, hover `#1D4ED8`, tint `#EFF6FF`, canvas `#F8FAFC`, surface `#FFFFFF`, text `#0F172A`, success `#10B981`, warning `#F59E0B`, danger `#EF4444`, plus the border and secondary text needed by the preview. Define named text/button/input/card/badge styles and spacing values used by the preview. Keep styles keyed rather than overriding every system control implicitly so existing `MainPage` and later WinUI controls retain expected behavior. The design board's 20/14/13/12 px text hierarchy and 8 px button/input radius, 4 px badge radius guide values; validate rendered metrics in Uno rather than assuming CSS pixels equal XAML effective pixels in every font. Treat bright semantic colors as accents; badge text needs separate darker foreground tokens measured to at least 4.5:1 on its tinted background. Verify actual hover, pressed, disabled, keyboard-focus, and input-error rendering in the running app: declaring state brushes without connecting them to rendered states is insufficient. Prefer the native control template's existing visual states; if keyed setters suppress or fail to customize the intended state, use the smallest Uno/WinUI-compatible state/template change that preserves keyboard focus and disabled behavior. Record observed colors and focus treatment, not merely the resource keys.

Alternative: load the HTML in a WebView or copy inline colors across pages. That would break native control behavior or create inconsistent future screens. Another alternative is a new design-system package; UI-00 does not justify that dependency.

### 2. Preview stays in the current placeholder

Keep `Views/Theme/ThemePreviewView.xaml` with minimal code-behind for XAML initialization and ViewModel resolution inside the existing `MainPage` content area. Retain its `NavigationView` menu and startup route in `App.xaml.cs`. Show color samples, typography, a card, buttons, input focus/error examples, success/warning/danger/neutral badges, loading/empty/error/success feedback, currency output, an icon, and an image fallback placeholder. The local `ViewModels/Theme/ThemePreviewViewModel.cs` holds only preview state and a retry command that changes the demonstration message; DI resolves it without business repositories. Give each feedback example a visible label; the retry command changes only the local error example. The preview now uses at most two swatch/button/badge columns and stacks inputs and feedback. Review the actual available width after the navigation pane and padding by narrowing the running window to a size the user can still use; scroll to every section and check that content remains reachable without horizontal clipping. A vertical `ScrollViewer` alone does not prove this behavior. Demo text explicitly says the feedback is a preview, so a success sample cannot be mistaken for a completed sale.

Alternative: create `ThemePreviewPage` with a new navigation route. UI-01 owns navigation changes, and a `UserControl` is enough for inspection now.

### 3. Keep native control states and readable text

Use WinUI control templates/states only where needed to match D31; avoid restyling controls so heavily that keyboard focus, pointer, or disabled behavior disappears. Show validation with text as well as a red border; status badges use text and bordered tinted backgrounds, not color alone. Use `SymbolIcon` or existing vector resources for the preview's icon, and a simple native placeholder when no product image exists. An image CDN pipeline waits for product screens.

Alternative: use a third-party icon package or custom bitmap assets immediately. D31's first preview does not require either.

### 4. Font and currency policy

Prefer Inter only if a local distributable font file and license are present at implementation time. None is present in the current client assets, so plan to use the platform's system sans-serif with Vietnamese glyph support for UI-00 and record the font difference in review notes. Do not fetch a remote font at runtime. Implement `Converters/CurrencyConverter.cs` as presentation-only formatting using a controlled `vi-VN` format; numeric models remain numeric. Null/unavailable input displays `—`, and the preview verifies `34990000` renders as `34.990.000 ₫`.

Alternative: bundle a font downloaded from D31's Google Fonts HTML or represent money as a string in the model. Both introduce unnecessary coupling or asset/license uncertainty at this stage.

## Risks / Trade-offs

- **Uno desktop and Windows can render XAML styles differently** → Build desktop and let the project owner inspect the running preview at their available display settings; verify Windows when a Windows environment is available and report that status explicitly.
- **D31 HTML and image disagree on badge shape** → Follow the visible board and its 4 px rectangular instruction; record this selection so later screens stay consistent.
- **System font differs from Inter** → Check Vietnamese wrapping and contrast in the preview; switch to a properly licensed bundled font in a later isolated change if needed.
- **Global resources could affect starter navigation** → Use keyed styles and inspect the existing menu after merging; keep `App.xaml.cs` and navigation route unchanged.
- **Static preview could be read as a real business status** → Label examples as preview/demo and make retry a local visual interaction only.
- **An OpenSpec checkbox was checked before visual acceptance was recorded** → Keep the relevant checks open until the project owner confirms the preview after inspecting normal and narrower windows, and record what they checked and any limitations in the handoff. Screenshots remain optional supporting material.

## Migration Plan

Add the resource dictionary and preview, then run and visually inspect the existing app. This change needs no data migration. Revert its merge and preview host to return to the starter visuals; later UI-01 can move the preview to a development-only surface without rewriting theme resources.

## Verification and handoff

From the repository root on Linux/macOS, restore if needed, then build and run the Uno Skia Desktop target:

```bash
dotnet restore TechStore.sln
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop --launch-profile "TechStore.Client (Desktop)"
```

The desktop preview is local and does not need PostgreSQL or the API. On Windows, verify the Windows target separately with `dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100` and its compatible WinAppSDK launch profile. A Linux desktop build does not validate native Windows rendering.

For final acceptance, the project owner opens the running preview at their available desktop scaling, checks it in a normal and a narrower usable window, and scrolls through all sections while comparing the theme with D31/D01. They exercise hover, press, disabled, keyboard Tab focus, retry, and text entry, then explicitly confirm whether the visual and interaction checks pass. Record the owner's confirmation, date, checked behavior, and any unverified behavior in the UI-00 handoff; screenshots are optional and no exact pixel size or display-scale percentage is required for this change. Record the exact desktop build/run commands and outcomes after the final source changes, separating DevServer warnings from application failures. Measure badge text contrast rather than inferring it from appearance. Do not mark a command's timed process termination as successful visual inspection. Windows may remain explicitly unverified on a non-Windows host under task 3.4. This UI-00 acceptance choice is narrower than the numeric examples in `docs/UI_IMPLEMENTATION_PLAN.md` section 6; later screens retain their own review criteria.
