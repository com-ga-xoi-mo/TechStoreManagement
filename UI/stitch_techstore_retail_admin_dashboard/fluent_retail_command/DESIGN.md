---
name: Fluent Retail Command
colors:
  surface: '#f7f9fb'
  surface-dim: '#d8dadc'
  surface-bright: '#f7f9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f6'
  surface-container: '#eceef0'
  surface-container-high: '#e6e8ea'
  surface-container-highest: '#e0e3e5'
  on-surface: '#191c1e'
  on-surface-variant: '#434655'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f3'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#505f76'
  on-secondary: '#ffffff'
  secondary-container: '#d0e1fb'
  on-secondary-container: '#54647a'
  tertiary: '#4d556b'
  on-tertiary: '#ffffff'
  tertiary-container: '#656d84'
  on-tertiary-container: '#eef0ff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#d3e4fe'
  secondary-fixed-dim: '#b7c8e1'
  on-secondary-fixed: '#0b1c30'
  on-secondary-fixed-variant: '#38485d'
  tertiary-fixed: '#dae2fd'
  tertiary-fixed-dim: '#bec6e0'
  on-tertiary-fixed: '#131b2e'
  on-tertiary-fixed-variant: '#3f465c'
  background: '#f7f9fb'
  on-background: '#191c1e'
  surface-variant: '#e0e3e5'
typography:
  display:
    fontFamily: Inter
    fontSize: 1.875rem
    fontWeight: '700'
    lineHeight: 2.25rem
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 1.5rem
    fontWeight: '600'
    lineHeight: 2rem
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 1.25rem
    fontWeight: '600'
    lineHeight: 1.75rem
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 1.125rem
    fontWeight: '600'
    lineHeight: 1.5rem
  body-lg:
    fontFamily: Inter
    fontSize: 1rem
    fontWeight: '400'
    lineHeight: 1.5rem
  body-md:
    fontFamily: Inter
    fontSize: 0.875rem
    fontWeight: '400'
    lineHeight: 1.375rem
  body-sm:
    fontFamily: Inter
    fontSize: 0.8125rem
    fontWeight: '400'
    lineHeight: 1.25rem
  label-md:
    fontFamily: Inter
    fontSize: 0.875rem
    fontWeight: '500'
    lineHeight: 1.25rem
  label-sm:
    fontFamily: Inter
    fontSize: 0.75rem
    fontWeight: '600'
    lineHeight: 1rem
    letterSpacing: 0.02em
  mono-data:
    fontFamily: Inter
    fontSize: 0.8125rem
    fontWeight: '500'
    lineHeight: 1.25rem
    letterSpacing: 0.01em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 1rem
  gutter-desktop: 1.5rem
  margin: 1rem
  margin-desktop: 2rem
  space-2xs: 0.25rem
  space-xs: 0.375rem
  space-sm: 0.5rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
  space-2xl: 3rem
---

## Brand & Style

This design system establishes a high-precision, utilitarian retail administration environment tailored for managing fast-moving consumer electronics inventories (smartphones, laptops, accessories, and warranty lifecycles). Inspired by Windows 11 Fluent Design principles, it delivers an institutional desktop-grade web application experience: clean geometry, restrained flat surfaces, ultra-crisp micro-interactions, and a structured layout optimized for sustained daily productivity.

The design language balances visual quietness with data density. Rather than utilizing heavy drop-shadows, blurred layers, or decorative color gradients, the system achieves hierarchy and separation exclusively through disciplined structural lines, alternating background tones, and purposeful typographic weights. The tone is authoritative, dependable, and swift—giving inventory controllers, store managers, and point-of-sale operators full situational awareness with minimal eye strain. Full localization into Vietnamese requires thoughtful horizontal rhythm to handle extended diacritic vertical bounds and multi-word nominal compounds effortlessly.

## Colors

The palette is rooted in functional clarity, built strictly around a flat, high-contrast light model:

- **Primary Canvas & Surfaces:** The default application canvas uses `#F8FAFC` (Slate 50), providing a calm, non-glare foundation. Data containers, panels, and data tables elevate cleanly with pure `#FFFFFF`.
- **Primary Interactive Accent:** `#2563EB` (Cobalt Blue) drives primary focal points, affirmative actions, focus rings, and active navigation items. Hover states shift to `#1D4ED8`; active pressed states resolve to `#1E40AF`.
- **Text & Visual Hierarchy:**
  - Primary text: `#0F172A` (Slate 900) ensures AAA accessibility across headings, primary metrics, and core data table cells.
  - Secondary / Meta text: `#64748B` (Slate 500) manages table headers, secondary descriptors, SKU metadata, timestamps, and input placeholders.
- **Structural Outlines:** All card boundaries, table cell dividers, and input borders utilize a crisp 1px rule in `#E2E8F0` (Slate 200). Subtle hover-stroke enhancements utilize `#CBD5E1` (Slate 300).
- **Functional Semantics (Vietnamese Status Indications):**
  - **Success (`Thành công` / `Còn hàng`):** `#16A34A` with a soft tint `#F0FDF4` and border `#BBF7D0`.
  - **Warning (`Chờ xử lý` / `Sắp hết hàng`):** `#F59E0B` with tint `#FFFBEB` and border `#FDE68A`.
  - **Danger (`Hết hàng` / `Hủy đơn`):** `#DC2626` with tint `#FEF2F2` and border `#FECACA`.
  - **Informational (`Đang giao`):** `#0284C7` with tint `#F0F9FF` and border `#BAE6FD`.

## Typography

The typography is powered exclusively by **Inter** across all roles, configured with OpenType features (`tnum` for tabular lining numbers, `cv05`, and `cv11`) to ensure strict alignment across financial figures (VND), stock units, and serial barcodes.

Because Vietnamese typography features complex accent clusters and diacritical marks (e.g., *ế, ồ, ặ, ỹ*), line heights are calibrated slightly taller than standard Western proportions (minimum 1.35x–1.5x) to eliminate clipping across dense data grids.

- **Display & Headlines:** Used strictly for administrative views, dashboard overviews (`Tổng quan kinh doanh`), and section heads. Tighter tracking creates a structured desktop software feel.
- **Body & Labels:** `body-md` (14px) serves as the primary reading and operational standard. `label-sm` (12px, semi-bold) is enforced for table headers, metadata descriptors, and status chips, always keeping casing legible without forced aggressive capitalization.
- **Numbers & Metrics:** Price points (e.g., `24.990.000 ₫`) and IMEI/serial numbers inherit tabular numeric features directly to prevent column jitter when values update in real-time.

## Layout & Spacing

The layout is built upon an 8-pixel linear progression system with a strict 4-pixel sub-grid for micro-alignments (e.g., icon alignments, input badges).

- **Grid Architecture:** 
  - Desktop environments run on a responsive 12-column layout with fixed left-hand utility navigation (240px default, 68px collapsed icon-rail).
  - Main administrative canvas adheres to fluid layout principles, stretching with constraints: data views expand to 100% viewport width while form sheets and detail inspectors lock to a maximum centered container of `1280px` or a dedicated right-side offcanvas flyout (440px wide).
  - Canvas page padding is `margin-desktop` (32px / 2rem), reducing to `margin` (16px / 1rem) on mobile viewports.
- **Rhythm & Density:**
  - Standard card interior padding is fixed at `1.25rem` (20px) or `1.5rem` (24px) for metric summaries.
  - Table rows follow a 44px default height rhythm (comfortably compact for catalog analysis) and 36px in compact high-density stocktaking views.
  - Form groups operate with a consistent 16px vertical gap (`space-md`), placing field labels 6px (`space-xs`) above input controls.

## Elevation & Depth

In conformance with high-performance desktop tools and modern Windows 11 Fluent guidelines, the design system minimizes visual noise by avoiding dropped blur shadows and diffusion layers:

1. **Zero Shadow Baseline:** Surfaces sit level on the canvas, separated solely by background tonal contrast (`#FFFFFF` on `#F8FAFC`) and 1px borders using `#E2E8F0`.
2. **Layering & Planes:**
   - **Base Canvas (Level 0):** `#F8FAFC`. Houses the global workspace.
   - **Static Containers (Level 1):** `#FFFFFF` bordered with `1px solid #E2E8F0`. Tables, card modules, charts, and filter panels.
   - **Interactive Hover & Transient Elements (Level 2):** Flyouts, dropdown menus, context popovers, and date pickers use a crisp border `1px solid #CBD5E1` combined with an imperceptible utility anchor: `box-shadow: 0 4px 6px -1px rgba(15, 23, 42, 0.06), 0 2px 4px -2px rgba(15, 23, 42, 0.04)`.
   - **Modal Dialogs & Command Palettes (Level 3):** Modal overlays employ a solid, semi-opaque backdrop (`rgba(15, 23, 42, 0.4)`) without backdrop-filter/blur. Modals sit crisp with a defined 1px `#CBD5E1` border and structured outer boundary `0 20px 25px -5px rgba(15, 23, 42, 0.1)`.

## Shapes

The geometric signature uses strict, low-radius architectural curvatures matching Fluent desktop UI standards:

- **Cards & Data Modules:** Enforced at exactly **8px** radius (`border-radius: 8px`). This keeps large rectangular blocks sharp, professional, and structurally sound without looking harsh.
- **Controls & Form Elements:** Buttons, text fields, select menus, search boxes, and pagination items are fixed at **6px** radius (`border-radius: 6px`).
- **Micro-Indicators:** Badges, status tags (`Thẻ trạng thái`), and numerical pills use **4px** or full circular capsules (**9999px**) depending on context: tags with labels take 4px; count indicators and avatar badges take full pills.

## Components

### Buttons
- **Primary:** Background `#2563EB`, text `#FFFFFF`, border `1px solid transparent`, radius `6px`, height 36px (compact) or 40px (standard). Hover: `#1D4ED8`. Focus: `2px solid #FFFFFF` ring + `2px solid #2563EB`. Active: `#1E40AF`.
- **Secondary / Outline:** Background `#FFFFFF`, text `#0F172A`, border `1px solid #E2E8F0`, radius `6px`. Hover: background `#F8FAFC`, border `#CBD5E1`.
- **Subtle / Ghost:** Background `transparent`, text `#64748B`. Hover: background `#F1F5F9`, text `#0F172A`.
- **Destructive:** Background `#DC2626`, text `#FFFFFF`. Hover: `#B91C1C`.

### Input Fields & Controls
- **Text Inputs & Selects:** Height 38px, background `#FFFFFF`, border `1px solid #E2E8F0`, radius `6px`, padding `0 12px`. Text is `#0F172A`, placeholder is `#94A3B8`. Hover: border `#CBD5E1`. Focus: border `#2563EB`, outline `1px solid #2563EB`. Disabled: background `#F1F5F9`, text `#94A3B8`, border `#E2E8F0`.
- **Search Bar (Quick Search / Tìm kiếm):** Prefixed with a 16px search icon in `#64748B`, includes integrated clear action (`✕`), width responsive or fixed 320px in top toolbar.

### Selection Controls (Checkboxes & Radios)
- **Checkboxes:** Size 16px × 16px, radius 4px, border `1px solid #CBD5E1`, background `#FFFFFF`. Checked state: background `#2563EB`, border `#2563EB` with white checkmark.
- **Radio Buttons:** Size 16px × 16px, circle radius, border `1px solid #CBD5E1`. Selected state: border `#2563EB`, inner solid center `#2563EB` (6px diameter).

### Status Chips & Badges
- Displayed with text `label-sm` (12px, font-weight 600), radius `4px`, padding `2px 8px`, border `1px solid`.
- **Còn hàng / Hoàn tất (In Stock / Complete):** Text `#15803D`, background `#F0FDF4`, border `#BBF7D0`.
- **Sắp hết / Đang chờ (Low Stock / Pending):** Text `#B45309`, background `#FFFBEB`, border `#FDE68A`.
- **Hết hàng / Đã hủy (Out of Stock / Canceled):** Text `#B91C1C`, background `#FEF2F2`, border `#FECACA`.
- **Thiết bị (Device Category Tag):** Text `#475569`, background `#F1F5F9`, border `#E2E8F0`.

### Cards & Metrics
- Background `#FFFFFF`, border `1px solid #E2E8F0`, radius `8px`, zero box-shadow.
- Metric cards display label in `body-sm` (`#64748B`), large figure in `headline-lg` (`#0F172A`, tabular numbers), accompanied by inline percentage trend badges (e.g., `+12.5% so với tháng trước`).

### Data Tables (Bảng dữ liệu hàng hóa)
- Header row (`<thead>`): Height 40px, background `#F8FAFC`, border-bottom `1px solid #E2E8F0`. Text is `label-sm` (`#64748B`, uppercase tracking).
- Body rows (`<tbody>`): Height 44px to 52px, background `#FFFFFF`, border-bottom `1px solid #F1F5F9`. Hover state on row: `#F8FAFC`.
- Cells: Text in `body-md` (`#0F172A`). Currency formatted with right-aligned `mono-data` (e.g., `18.490.000 ₫`).

### Specialized Retail Components
- **IMEI / Serial Inspector Badge:** Compact monospaced capsule with copy-to-clipboard action icon for quick logistics scanning.
- **Inventory Stock Bar:** 4px high track (`#E2E8F0`) with dynamic fill reflecting real-time stock limits (green > 20%, amber 5-20%, red < 5%).