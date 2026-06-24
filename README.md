# Regkasse POS System

RKSV-compliant POS system with multi-tenant architecture.

## Quick Start

```bash
# Backend
cd backend && dotnet run

# Admin Panel
cd frontend-admin && npm run dev

# POS App
cd frontend && npm start
```

## Tech Stack

| Component | Version |
|-----------|---------|
| Backend | .NET 10 / EF Core 10 |
| Admin Panel | Next.js 16 / React 19 / Ant Design 6 |
| POS App | Expo SDK 56 / React Native 0.85 |
| Database | PostgreSQL |

## Documentation

| Area | Link |
|------|------|
| Backend | [`backend/README.md`](backend/README.md) |
| Admin Panel | [`frontend-admin/README.md`](frontend-admin/README.md) |
| POS App | [`frontend/README.md`](frontend/README.md) |
| AI Onboarding | [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md) |
| Agent rules | [`AGENTS.md`](AGENTS.md) |
| Billing / mandant license | [`docs/BILLING_TENANT_LICENSE.md`](docs/BILLING_TENANT_LICENSE.md) |

## License Management

### Super Admin

- **License Overview:** `/admin/license` — View server license and all tenant licenses
- **License Sales:** `/admin/billing` — Manage license sales and statistics
- **New Sale:** `/admin/billing/sales/new` — Create a new license sale with PDF invoice

### Manager

- **License Status:** `/admin/license` — View own tenant license status
- **Extend License:** Enter license key to extend tenant license

## License

Proprietary — All rights reserved.
