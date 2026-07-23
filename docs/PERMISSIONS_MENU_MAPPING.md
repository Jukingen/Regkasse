# Menü → Permission Group Mapping

Cross-check between the FA sidebar IA and the role/permissions catalog groups.

| Source | Location |
|--------|----------|
| Sidebar layout | `frontend-admin/src/shared/adminSidebarRegistry.ts` (`SIDEBAR_LAYOUT_ROWS`) |
| Route / menu gates | `frontend-admin/src/shared/auth/routePermissions.ts` |
| Catalog groups | `backend/Authorization/PermissionCatalogMetadata.cs` |
| Group order / i18n | `users.roleDrawer.groups.*` · `PERMISSION_GROUP_ORDER` |
| Role defaults | [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) |

**Last verified:** 2026-07-22

Canonical permission keys use `resource.action` (e.g. `user.view`, `cash_register.manage`, `daily-closing.view`). There are **no** `users.*`, `cashregister.*`, `dailyclosing.*`, `session.*`, or `paymentmethod.*` catalog keys.

### Key consistency gate

| Source | Path |
|--------|------|
| Backend catalog | `backend/Authorization/AppPermissions.cs` |
| FA typed catalog | `frontend-admin/src/shared/auth/permissions.ts` (entrypoint: `permissionsCatalog.ts`) |
| Route gates | `frontend-admin/src/shared/auth/routePermissions.ts` |
| Sidebar catalog `permission` | `frontend-admin/src/shared/adminSidebarRegistry.ts` |
| IA menu registry | `frontend-admin/src/shared/auth/menuPermissionRegistry.ts` |
| Permission groups ↔ sidebar | `frontend-admin/src/shared/auth/permissionGroupRegistry.ts` |

```bash
npm run verify:permission-keys          # Backend ↔ FA catalog ↔ route/sidebar key strings
npm run verify:permission-keys:table    # full Backend | FA | Route | Menu mapping
npm run verify:menu-permissions         # every sidebar/RKSV leaf has ROUTE_PERMISSIONS + group sync
npm run verify:menu-permissions:secondary  # + Backup/Settings secondary-nav paths
```

Debug menu↔group gaps in the Admin sidebar: set `NEXT_PUBLIC_DEBUG_MENU_PERMISSION_GROUPS=true` or open FA with `?debugMenuPermissions=1`.

| Backend Key | FA Catalog | Route | Menu |
|-------------|------------|-------|------|
| `daily-closing.view` | `daily-closing.view` | `daily-closing.view` | `daily-closing.view` |
| `daily-closing.execute` | `daily-closing.execute` | `daily-closing.execute` | — (execute route only; menu uses view) |

FA may omit POS-only / rarely used backend keys (typed subset). Use `--strict-fa-complete` to require full parity.

---

## Primary mapping (sidebar → permission group)

| Menü (Sidebar, de) | Permission group (catalog) | Group slug | Catalog keys (prefixes) | Typical route gate(s) |
|--------------------|----------------------------|------------|-------------------------|------------------------|
| **Dashboard** / Übersicht | — (no dedicated group) | — | — | authenticated (`/dashboard`) |
| **Lizenzverwaltung** | Einstellungen *(license)* · Digitale Dienste *(nested)* | `einstellungen` / `digitale_dienste` | `license.*` → Einstellungen; `digital.*` | Super Admin / digital permissions |
| **Operations Center** | — (no dedicated group) | — | — | multi-permission OR (ops hub) |
| **Tische** | Bestellung & Verkauf | `bestellung_verkauf` | `table.*` | `table.view` |
| **Kassenverwaltung** | Kassenverwaltung | `kassenverwaltung` | `cash_register.*`, `cashdrawer.*` | `cash_register.manage` |
| **Mitarbeiter** | Mitarbeiter | `mitarbeiter` | `user.*`, `role.*` | hub: `user.view` ∨ `report.view` ∨ `shift.view`; CRUD: `/admin/users` + `user.manage` |
| **Schichten & Abschlüsse** | Kassenverwaltung *(shift)* · Tagesabschluss *(closing)* | `kassenverwaltung` / `tagesabschluss` | `shift.*`; closings use `daily-closing.*` | `shift.view` on `/shifts` |
| **Verkauf & Vorgänge** (nested) | Bestellung & Verkauf · Zahlung · Digitale Dienste · Sonstige | several | see leaf table below | per leaf |
| **Tagesabschluss** | Tagesabschluss | `tagesabschluss` | `daily-closing.*` | `daily-closing.view` / `.execute` |
| **RKSV & FinanzOnline** | RKSV & FinanzOnline · Audit & Berichte *(some leaves)* | `rksv_finanzonline` / `audit_berichte` | `rksv.*`, `finanzonline.*`, `tse.*`; audit leaves → `audit.*` | per RKSV / audit route |
| **Sortiment & Preise** | Sortiment & Preise · Lager | `sortiment_preise` / `lager` | `product.*`, `category.*`, `modifier.*`; inventory → `inventory.*` | `product.view`, `category.view`, `inventory.view` |
| **Kunden & Vorteile** | Kunden & Vorteile | `kunden_vorteile` | `customer.*`, `benefit.*` | customer / benefit routes |
| **Berichte & Auswertungen** | Audit & Berichte | `audit_berichte` | `report.*`, `audit.*` | `report.view`, `audit.view` |
| **Backup & Disaster Recovery** | Backup & Disaster Recovery | `backup_disaster_recovery` | `backup.*`, `settings.backup` | reads: `settings.view`; manage: `backup.manage` |
| **Einstellungen** | Einstellungen | `einstellungen` | `settings.*`, `website.*`, `localization.*`, `receipttemplate.*` | mostly `settings.view` / `.manage` |
| **Digitale Dienste** (under Einstellungen / Lizenz) | Digitale Dienste | `digitale_dienste` | `digital.*` | `digital.view` / `website.manage` / … |
| **Sitzung & Inaktivität** | Einstellungen *(no `session.*` keys)* | `einstellungen` | — | `settings.view` |
| **Zahlungsarten** | Einstellungen *(no `paymentmethod.*` keys)* | `einstellungen` | — | `settings.view` (gateway: `settings.manage`) |
| **Verwaltung** | Mitarbeiter · System | `mitarbeiter` / `system` | `user.*`, `role.*`, `tenant.*`, `system.*` | Access hub + Super Admin leaves |

---

## Verkauf & Vorgänge — leaf detail

| Sidebar leaf | Permission group | Keys / gate |
|--------------|------------------|---------------|
| Belege | Bestellung & Verkauf | `sale.view` |
| Online-Bestellungen | Digitale Dienste *(+ order)* | `digital.orders.view` ∨ `order.view` |
| Zahlungen | Zahlung | `payment.view` |
| Zahlungstrends / Karte / Storno | Zahlung | `payment.view` |
| Gutscheine | Zahlung | `voucher.read` / `.create` / … |
| Rechnungen | Bestellung & Verkauf | `invoice.view` |

---

## Catalog groups without a 1:1 top-level sidebar label

| Permission group | Slug | Notes |
|------------------|------|--------|
| **Zahlung** | `zahlung` | Sidebar nests payments under **Verkauf & Vorgänge**, not a top-level group. |
| **Lager** | `lager` | Sidebar nests inventory under **Sortiment & Preise**. |
| **System** | `system` | Super Admin / platform only (`system.critical`, tenant ops). |
| **Sonstige** | `sonstige` / `other` | Catch-all (`price.override`, `receipt.reprint`). |

---

## Gaps and alignment notes

### Already gated (no missing group required)

These menus have **route/sidebar permission checks** even when there is no same-named catalog group:

| Menu | Gate | Catalog home |
|------|------|----------------|
| Operations Center | OR of ops permissions | N/A (hub) |
| Tische | `table.view` | Bestellung & Verkauf |
| Sitzung & Inaktivität | `settings.view` | Einstellungen |
| Zahlungsarten | `settings.view` | Einstellungen |
| Zahlungs-Gateway | `settings.manage` | Einstellungen |
| Darstellung & Sprache | `settings.view` | Einstellungen |
| Dashboard | authenticated | N/A |

**Do not invent** `session.*` or `paymentmethod.*` keys; settings gates are intentional.

### Gaps to watch

| Issue | Current state | Recommendation |
|-------|---------------|----------------|
| **`voucher.*` → Zahlung** | Gutscheine under Verkauf; catalog group **Zahlung** | Aligned (2026-07-22). |
| **Schichten vs Tagesabschluss** | Shifts stay in **Kassenverwaltung**; daily closing is its own group | Keep as-is: matches separate sidebar leaves (`/shifts` vs `/tagesabschluss`). |
| **Online-Bestellungen** | Menu under Verkauf; permissions under **Digitale Dienste** | Keep as-is (non-fiscal `digital.orders.*`); document cross-link in UI if needed. |
| **Audit Logs under RKSV** | Sidebar under RKSV; keys in **Audit & Berichte** | Acceptable; fiscal ops vs audit reporting split. |
| **Backup reads** | Hub uses `settings.view`; manage uses `backup.manage` | Documented in [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md); no `backup.view` key by design. |
| **Lizenzverwaltung** | No `license.*` permission group | License keys live under **Einstellungen**; Super Admin surfaces are role/`system` gated. |

### Intentionally no dedicated permission group

| Menu | Why |
|------|-----|
| Dashboard / Overview | Any authenticated admin session |
| Operations Center | Aggregator; leaf routes carry real gates |
| Verwaltung (shell) | Nested Access / Super Admin leaves carry gates |
| Nested labels (Sonderbelege, Backup-Konfiguration, …) | Children are gated individually |

---

## Permission group display order (roles UI)

Must stay aligned with `PERMISSION_GROUP_ORDER`:

1. Mitarbeiter  
2. Kassenverwaltung  
3. Bestellung & Verkauf  
4. Zahlung  
5. Tagesabschluss  
6. RKSV & FinanzOnline  
7. Sortiment & Preise  
8. Lager  
9. Kunden & Vorteile  
10. Audit & Berichte  
11. Backup & Disaster Recovery  
12. Einstellungen  
13. Digitale Dienste  
14. System / Sonstige  

Sidebar top-level order for comparison: Dashboard → Lizenzverwaltung → Betrieb → RKSV → Sortiment → Kunden → Berichte → Backup → Einstellungen → Verwaltung.

---

## Verification checklist

- [ ] Every visible sidebar leaf has `ROUTE_PERMISSIONS[menuKey]` (enforced by `sidebarRouteCoverage` tests + `npm run verify:menu-permissions`).
- [ ] Catalog group labels in `users.roleDrawer.groups.*` match sidebar terminology (de/en/tr).
- [ ] New menu areas get either a catalog group remap **or** an existing gate (`settings.view`, etc.) — avoid orphan menus.
- [ ] Do not add parallel permission keys (`users.view`, `dailyclosing.manage`, …) that diverge from `AppPermissions`.
- [ ] New IA areas in `menuPermissionRegistry` include a primary path that exists in `ROUTE_PERMISSIONS`.

---

## Related docs

- [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) — role × permission defaults  
- [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) — backup RBAC  
- [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) / [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md)  
- FA Access hub: `frontend-admin/docs/ACCESS_AND_ROLES_HUB.md`
