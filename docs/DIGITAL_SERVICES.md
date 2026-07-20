# Digital Services — User & Operator Guide

> **Scope:** Tenant website / mobile-app generation, Mandanten requests, and **non-fiscal** online orders from website/PWA/native.  
> **Not in scope:** POS carts, TSE signing, RKSV receipts, `payment_details` fiscal chain.

**Last updated:** 2026-07-19

**Always-applied rules:** [`AGENTS.md`](../AGENTS.md) § Roles (Digital services & online orders) · § Working hours (website/app only)  
**Online orders guide:** [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md)  
**Working hours scope:** [`WORKING_HOURS.md`](WORKING_HOURS.md) — online intake only; never POS/FA  
**Feature changelog:** [`CHANGELOG.md`](CHANGELOG.md)  
**AI onboarding:** [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) § Digital Services  
**Permissions source of truth:** `backend/Authorization/RolePermissionMatrix.cs`, `AppPermissions.cs` · matrix doc: [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md)

---

## Overview

Digital services let each mandant (tenant) run a **customer-facing website** and optional **mobile app** (PWA / Native package) driven by catalog and company data. Operators manage this in **Frontend-Admin (FA)** only — never on the POS terminal.

| Surface | Who | Purpose |
|---------|-----|---------|
| Website | Super Admin creates/publishes; Manager previews/requests | Public site + optional online ordering |
| Mobile app | Super Admin generates packages; Manager previews/requests | PWA or Expo Native ZIP |
| Online orders | Manager status workflow; Super Admin full control | Kitchen/fulfillment inbox — **not** fiscal |

---

## Architecture (do not conflate)

```mermaid
flowchart TB
  subgraph public [Customer]
    WEB[Website / PWA / App]
  end
  subgraph fa [Frontend-Admin]
    MGR[Manager portal]
    SA[Super Admin digital hub]
  end
  subgraph api [API]
    OO[online_orders]
    WW[/api/admin/website/*]
    DIG[/api/admin/digital/*]
    ORD[/api/admin/online-orders/*]
  end
  subgraph pos [POS / RKSV — separate]
    CART[POS cart / payment]
    TSE[TSE / fiscal receipt]
  end
  WEB --> OO
  MGR --> DIG
  MGR --> ORD
  SA --> WW
  SA --> DIG
  OO -.->|optional approve bridge only| CART
  CART --> TSE
```

- Table: `online_orders` (+ items / status history).
- Status API: `PATCH /api/admin/online-orders/{id}/status` (`digital.orders.manage`).
- Optional POS cart bridge: `POST /api/admin/online-orders/{id}/accept` requires `digital.orders.approve` (**Super Admin**). Manager fulfillment is **status-only**.
- **Working hours** gate website/app order intake only (`GET /api/sites/{slug}/status`, `POST /api/public/online-orders`) — never POS cart/payment or FA access. Detail: [`WORKING_HOURS.md`](WORKING_HOURS.md).

---

## Permissions (summary)

| Permission | Manager | Super Admin |
|------------|---------|-------------|
| `digital.view` / `preview` / `request` | Yes | Yes (via catalog / `digital.manage`) |
| `digital.create` / `publish` / `edit` / `delete` / `manage` | No | Yes |
| `digital.orders.view` / `manage` | Yes | Yes |
| `digital.orders.approve` (POS bridge) | No | Yes |
| `website.manage` | Yes (domains/customization) | Yes |

Implication: `digital.manage` satisfies simplified + legacy digital keys and all `digital.orders.*`.  
`website.manage` implies view/preview/request only (not create/publish).

---

## For Mandanten-Admin (Manager)

### Viewing your website / app

1. Open FA → **Einstellungen** → **Digitale Dienste** (`/settings/digital`), or use the digital portal entry (`nav.digitalPortal`).
2. Check website / app provision status (none / pending / created / published).
3. Open **Vorschau** → `/tenant/{tenantId}/website-preview` (template preview; read-only).
4. When published, open the public URL from the status card if present.

### Requesting creation or template change

1. FA → Digitale Dienste.
2. Click **Erstellung anfordern** / send request (`digital.request`).
3. Super Admin reviews the queue (`/admin/digital/requests`). Approve does **not** auto-generate — Super Admin still runs the generator on the tenant digital page.
4. Template change requests may include a note such as `template-change:{templateId}`.

Typical SLA copy in UI: 1–2 business days (i18n).

### Managing online orders

1. FA → **Online-Bestellungen** (`/orders/online`), or deep link `/tenant/{tenantId}/orders`.
2. Filter by status badges: pending / accepted / preparing / ready / completed.
3. Advance status with the next-step action (or cancel from detail):

   `pending` → `accepted` → `preparing` → `ready` → `completed`

4. Skipped steps are rejected by the API (`ONLINE_ORDER_STATUS_TRANSITION_INVALID`).

**Does not:** create POS carts, TSE signatures, or fiscal receipts.

---

## For Super Admin

### Creating a website

1. FA → **Digitale Dienste (Verwaltung)** (`/admin/digital`) or tenant page `/tenant/{id}/digital`.
2. Select / open the target tenant.
3. Choose a template from the generator (API list: `GET /api/admin/website/templates`).
4. **Shipped templates:** Modern, Classic, Minimal. (i18n may show extra labels for future templates.)
5. Run **Website erstellen** (`digital.create` / generator). Then publish when ready (`digital.publish`).

### Creating a mobile app

1. Same digital hub / tenant digital page.
2. Choose **PWA** or **Native**, generate package (`POST /api/admin/website/mobile/generate` or package download).
3. Native ZIP is built locally with Expo — the API does not produce store APK/IPA binaries.

### Managing requests

1. FA → **Digitale Dienst-Anfragen** (`/admin/digital/requests`).
2. Review pending requests; **approve** or **reject**.
3. After approve, create/publish artifacts on the tenant digital page.

### Domains & branding

- Custom domains / packages: `/tenant/{id}/domain`, `/settings/website` (`website.manage`).
- Branding / live preview: `/tenant/{id}/customize` (Super Admin–oriented).

### Online orders (Super Admin)

- Same FA inbox as Manager, plus `digital.orders.approve` for optional POS cart bridge.
- Prefer status-only fulfillment unless a deliberate POS handoff is required.

---

## FA routes (quick map)

| Route | Audience | Notes |
|-------|----------|--------|
| `/settings/digital` | Manager | Ambient JWT tenant portal |
| `/digital/customer-portal` | Manager | Customer digital services portal |
| `/orders/online` | Manager | Online-order inbox |
| `/tenant/{id}/website-preview` | Manager (+ SA) | Template preview + change request |
| `/tenant/{id}/orders` | Manager (+ SA) | Tenant-context orders deep link |
| `/tenant/{id}/digital` | SA (Manager request panel if opened) | Generators for Super Admin |
| `/admin/digital` | Super Admin | Cross-tenant digital management |
| `/admin/digital/requests` | Super Admin | Request queue |

i18n namespaces: `digital.*`, `tenants.digitalServices.*`, `onlineOrders.*`, `nav.digital*`, `adminShell.nav.*`.

---

## API cheat sheet

| Action | Method / path | Permission |
|--------|----------------|------------|
| List templates | `GET /api/admin/website/templates` | `digital.view` |
| Preview HTML | `POST /api/admin/website/preview` | (website/digital preview path) |
| Generate website | `POST /api/admin/website/generate` | create (Super Admin) |
| Request service | `POST /api/admin/digital/{tenantId}/request` | `digital.request` |
| List/approve requests | `/api/admin/digital/requests*` | `digital.manage` |
| List online orders | `GET /api/admin/online-orders` | `digital.orders.view` |
| Update status | `PATCH /api/admin/online-orders/{id}/status` | `digital.orders.manage` |
| Accept → POS cart | `POST /api/admin/online-orders/{id}/accept` | `digital.orders.approve` |

Cross-tenant access follows standard FA rules: **HTTP 404** (not 403).

---

## Related code

| Area | Location |
|------|----------|
| Permissions | `backend/Authorization/AppPermissions.cs`, `RolePermissionMatrix.cs`, `PermissionImplication.cs` |
| Online orders | `AdminOnlineOrdersController`, `OnlineOrderStatusService`, `Models/OnlineOrder.cs` |
| Website generator | `AdminWebsiteController`, `WebsiteGeneratorService` |
| FA Manager UI | `features/digital/*`, `features/orders/*`, `features/website-generator/*` |
| FA Super Admin | `features/digital-services/*`, `/admin/digital*` |

---

## Checklist for operators

- [ ] Manager never uses POS to “complete” a website order for RKSV — online status is enough for digital fulfillment.
- [ ] Super Admin does not treat approve-request as auto-publish; generation is a separate step.
- [ ] Do not document Bistro/Gourmet/Street Food as live generators until they appear in `GET /api/admin/website/templates`.
- [ ] Keep online-order work off TSE / DEP / Tagesabschluss paths.
