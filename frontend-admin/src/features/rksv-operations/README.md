# RKSV Operations (Admin)

**`/rksv`** — operations landing: light API summaries where they exist, links to full tools under `/rksv/*` and `/audit-logs`. No invented aggregates; CTA-only cards where there is no summary API.

**Code:** `components/RksvOperationsDashboard.tsx` (UI + queries), `normalizers.ts` (health + copy), `types.ts` (`OpsHealthLevel`).

**Environment label:** `NEXT_PUBLIC_RKSV_ENVIRONMENT` — strict states `TEST` | `PROD` | `UNCONFIGURED` | `INVALID` (build-time). **`next build` requires `TEST` or `PROD`** (enforced in `next.config.mjs`); dev without env shows UNCONFIGURED on `/rksv`. Parser: `src/shared/config/rksvEnvironment.ts`. UI: `components/RksvFinanzOnlineEnvironmentStatus.tsx` (`data-rksv-environment-state` for support/debug). Setup: root `README.md`, `.env.example`.
