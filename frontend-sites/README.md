# Regkasse Sites (`frontend-sites`)

Shared multi-tenant customer websites and online-order UI (Next.js App Router).

Not part of POS or FA fiscal flows. Public catalog and order intake only — working hours may gate **website** ordering; they must never gate POS/FA.

## Tech stack

| Layer | Choice |
|-------|--------|
| Framework | Next.js 16 (App Router) |
| UI | React 19 |
| Language | TypeScript |

## Setup

```bash
cd frontend-sites
cp .env.example .env.local   # NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
npm install
npm run dev                  # http://localhost:3001
```

From repo root: `npm run dev:sites`.

## Scripts

| Script | Description |
|--------|-------------|
| `npm run dev` | Next.js dev server on port **3001** |
| `npm run build` | Production build |
| `npm run start` | Serve production build on **3001** |
| `npm run typecheck` | `tsc --noEmit` |
| `npm run lint` | Typecheck (no separate ESLint suite yet) |
| `npm run test` | Same as typecheck until unit tests exist |

## Routes

- `/` — landing / entry
- `/[slug]` — tenant website by slug (catalog, hours, online order panel)

API: public/sites endpoints on the backend (`/api/sites/*`, `/api/public/online-orders`). See [`docs/DIGITAL_SERVICES.md`](../docs/DIGITAL_SERVICES.md) and [`docs/ONLINE_ORDERS.md`](../docs/ONLINE_ORDERS.md).

## Related

- Backend digital / online-order APIs
- FA: `/settings/digital`, `/orders/online`, `/admin/digital`
- [`AGENTS.md`](../AGENTS.md) — digital permissions & working-hours rules

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
