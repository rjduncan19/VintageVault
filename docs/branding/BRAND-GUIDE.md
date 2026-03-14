# VintageVault Brand Guide

## Official Logo

The official VintageVault logo is the **VV Nested Monogram**: two layered V chevrons with a cloud + lock, on a dark circle background.

**File:** [`logo.svg`](logo.svg) (also `logo-vv-nested.svg`)

### Logo Anatomy

```
┌─────────────────────────┐
│     Dark circle bg       │
│                         │
│    ╲   Cloud+Lock  ╱    │  ← Cloud with lock sits at
│     ╲    ☁🔒     ╱     │     the convergence point
│      ╲  ╲    ╱  ╱      │
│       ╲  ╲  ╱  ╱       │  ← Inner V (lighter blue)
│        ╲  ╲╱  ╱        │  ← Outer V (deeper blue)
│         ╲    ╱         │
│          ╲  ╱          │
│                         │
│       VINTAGE           │  ← Light text, bold weight
│        VAULT            │  ← Blue text, light weight
└─────────────────────────┘
```

### Color Palette

| Name | Hex | Usage |
|------|-----|-------|
| **Deep Navy** | `#0f172a` | Backgrounds, logo circle fill |
| **Slate Blue** | `#1e3a5f` | Logo gradient start |
| **Primary Blue** | `#2563eb` | CTAs, links, accent color |
| **Light Blue** | `#60a5fa` | Logo V gradient, secondary accent |
| **Pale Blue** | `#93c5fd` | Inner V, highlights |
| **Success Green** | `#059669` | Health indicators, checkmarks |
| **Warning Amber** | `#f59e0b` | Alerts, upgrade prompts |
| **Danger Red** | `#ef4444` | Errors, deleted file indicators |
| **White** | `#ffffff` | Cloud shape, text on dark |
| **Light Gray** | `#f8fafc` | Page backgrounds |
| **Slate Text** | `#1e293b` | Body text on light backgrounds |

### Typography

- **Headings:** System font stack (`-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif`)
- **Wordmark "VINTAGE":** Bold (700), light color (`#e2e8f0`), letter-spacing 4px
- **Wordmark "VAULT":** Light (300), blue (`#60a5fa`), letter-spacing 8px

### Logo Usage

| ✅ Do | ❌ Don't |
|-------|---------|
| Use on dark backgrounds | Stretch or distort |
| Maintain minimum padding (20% of logo width) | Place on busy/patterned backgrounds |
| Use the SVG for web (scales perfectly) | Rasterize at low resolution |
| Use icon-only (without wordmark) for favicons/system tray | Change the colors |

### Logo Variants

| File | Usage |
|------|-------|
| `logo.svg` | Official full logo (icon + wordmark) |
| _TODO:_ `logo-icon-only.svg` | System tray, favicon, small contexts |
| _TODO:_ `logo-horizontal.svg` | Navbar, horizontal layouts |
| _TODO:_ `logo-light-bg.svg` | For use on white/light backgrounds |

### Alternative Concepts (Archive)

The `docs/branding/` folder contains earlier concepts explored during the design process:
- `logo-shield-cloud.svg` — Shield concept
- `logo-vault-door.svg` — Vault door concept
- `logo-cloud-sync.svg` — App icon concept
- `logo-v-monogram.svg` — Single V (precursor to chosen VV)
- `logo-vv-side-by-side.svg` — Side-by-side VV variant
- `logo-vv-interlocked.svg` — Interlocked VV variant
- `logo-concepts.html` — Visual comparison of all concepts
