# Dashboard Widget Customization — Faz A/B Checklist & Inventory

**Status:** Faz A complete; Faz B (Lizenz + Handlungsbedarf) complete (2026-08-09)  
**Out of scope:** KPI strip as one mega-widget; order in `/api/admin/user/preferences`; second `@dnd-kit` stack; Faz C polish (reset defaults, SanitizeSettings `payment-trends.period`, hardcoded “Widgets anpassen”).

---

## 1. Architecture (source of truth)

| Layer | Location |
|-------|----------|
| DnD grid | `WidgetGrid.tsx` (`@dnd-kit`) |
| Persist API | `GET/POST /api/admin/dashboard/preferences` (`DashboardController`) |
| Catalog (BE) | `DashboardWidgetCatalog.cs` |
| Catalog (FE) | `DASHBOARD_WIDGET_IDS` + `widgetRegistry.tsx` |
| Client filter | `dashboardWidgetVisibility.ts` (`hasPermission` → `permissionImplied`) |
| Shells | `ManagerDashboard.tsx`, `SuperAdminDashboard.tsx` → `<Dashboard />` / `headerSlot` |

Preferences are **per user + effective tenant**.

---

## 2. Catalog widgets (reorderable / hideable)

| `widgetId` | Permission | DefaultVisible | Notes |
|------------|------------|----------------|--------|
| **`action-required`** | `daily-closing.view` | **true** | Tagesabschluss + RKSV reminders (Faz B) |
| `manager-license-status` | `license.view` | true | Rich Mandanten-Admin Lizenz card (Faz B) |
| `manager-kpi-strip` | `report.view` | true | |
| `manager-monatsbeleg` | `cash_register.view` | true | |
| `manager-activity` | `audit.view` | true | |
| `manager-tse-health` | `cash_register.view` | true | |
| `manager-offline-queue` | `payment.view` | true | |
| `manager-license-checklist` | `license.view` | true | |
| `manager-license-support` | `license.view` | true | |
| `manager-hospitality-links` | `cash_register.view` | false | |
| `manager-export-quick-actions` | `report.export` | false | |
| `today-sales` … `system-metrics` | (unchanged) | mostly true | |
| `license-expiry` | `license.manage` | **false** | Slim widget; off by default to avoid duplicate with `manager-license-status` |

Filter uses `PermissionImplication.IsSatisfied` (manage→view).

---

## 3. Fixed (non-DnD) surfaces

### Manager
- Welcome + cash register selector (feeds `useCashRegisterSelection` for widgets)
- Pending Monatsbeleg alert (not catalog)
- **No longer fixed:** Lizenz, Handlungsbedarf (Tagesabschluss/RKSV), KPI, Activity, TSE, Offline, etc.

### SuperAdmin (`headerSlot`)
- `LicenseDashboardSection`, Offline, TimeSync, TSE, hospitality, export
- **Removed from header:** `RksvReminderCard`, `DashboardMonatsbelegSection` (now catalog / settings)

---

## 4. Faz A QA checklist

### Manual
- [ ] Manager `/dashboard` — grid + **action-required** + **manager-license-status**
- [ ] Drag / visibility persist after reload
- [ ] SuperAdmin — no duplicate RKSV card in header; enable widgets via settings if needed
- [ ] `license-expiry` hidden by default; can enable in settings

### Automated
| Suite | Result |
|-------|--------|
| FA visibility / reorder / registry | Pass (incl. `action-required`) |
| BE `DashboardControllerTests` | Pass |
| BE `DashboardWidgetCatalogTests` | Pass (A1 + ActionRequired + license-expiry hidden) |

---

## 5. Regression findings

### A1 — Catalog filter ignores implications — **FIXED**
`FilterByPermissions` → `PermissionImplication.IsSatisfied`.

### A2 — `SanitizeSettings` only keeps `top-selling-products.period` — Faz C

### A3 — Hardcoded UX strings (`Widgets anpassen`, SA intro) — Faz C

### A4 — Duplicate license — **mitigated**
`license-expiry` `DefaultVisible=false`; rich card is `manager-license-status`. Existing prefs that already saved `license-expiry` visible keep that until user hides it.

### A5 — No “reset to defaults” — Faz C

---

## 6. Faz B delivered

### B1 Lizenz — **B1b**
- Catalog + registry + `ManagerLicenseStatusWidget`
- Fixed card removed from Manager shell
- `license-expiry` default off

### B2 Handlungsbedarf
- Catalog id `action-required`
- `ActionRequiredWidget` composes `TagesabschlussReminder` + `RksvReminderCard` with section-level gates
- Register via shared `useCashRegisterSelection`
- Removed pinned reminders from Manager; RKSV/Monatsbeleg removed from SA header

### Deferred
- Backup/TSE further alignment, Activity/Support polish, Faz C

---

## 7. Persist + merge

- GET merge: new catalog ids appended with **`IsVisible = def.DefaultVisible`**
- POST save: missing allowed widgets still appended **`IsVisible = false`**
- New users: `BuildDefaultLayout` uses each `DefaultVisible`

---

## Related

- [`api-contract.md`](./api-contract.md) § Dashboard  
- Backend: `DashboardController`, `DashboardWidgetCatalog`  
- FE: `features/dashboard/`
