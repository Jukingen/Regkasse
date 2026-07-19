# Changelog — Digital services & online orders

Feature-wave notes for website/app generation, digital requests, and non-fiscal online orders.

Canonical Keep a Changelog for the whole repo: [`CHANGELOG.md`](../CHANGELOG.md)  
Older engineering waves: [`CHANGELOG_RECENT.md`](CHANGELOG_RECENT.md)

---

## [1.0.0] - 2026-07-19

### Added

- **Digital Services:** Website and mobile app generation (Super Admin); Manager preview + creation/template-change requests
- **Online Orders:** Web/app order inbox with status lifecycle (`pending` → `accepted` → `preparing` → `ready` → `completed`, or `cancelled`)
- **Template preview** for Managers (`/tenant/{id}/website-preview`)
- **Request management** for Super Admin (`/admin/digital/requests`)
- **Permissions:** `digital.*` and `digital.orders.view` / `manage` / `approve`
- **Docs:** [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md), [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md), [`PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md), [`API_CONTRACTS.md`](API_CONTRACTS.md) (Digital services & online orders)
- Multi-language FA copy (DE, EN, TR) for digital / online-order surfaces (`digital.*`, `tenants.digitalServices.*`, `onlineOrders.*`)

### Changed

- Manager digital services: **view + preview + request** only (no create / publish / delete)
- Super Admin: full digital create / publish / edit / delete / request approve
- Online orders treated as **separate from POS** (status-only Manager fulfillment; optional POS cart bridge gated by `digital.orders.approve`)

### Fixed

- Tenant switcher JWT refresh in digital / multi-tenant admin flows
- Digital services permission checks (matrix, implication, FA route gates)
- Scoped service resolution for digital / website / order services (no root-provider DbContext misuse)

### Guides

| Doc | Audience |
|-----|----------|
| [DIGITAL_SERVICES.md](DIGITAL_SERVICES.md) | Website / app / requests |
| [ONLINE_ORDERS.md](ONLINE_ORDERS.md) | Order status workflow |
| [PERMISSIONS_MATRIX.md](PERMISSIONS_MATRIX.md) | Role × permission defaults |
| [AGENTS.md](../AGENTS.md) § Roles | Always-applied RBAC summary |
