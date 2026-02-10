You are working on the Regkasse project (Austria POS / Cash Register).
This file applies to all Agent Manager missions and live chats.

GLOBAL RULES:
- Follow ai/*.md documents in the repository.
- Do NOT ask about architecture or styling unless explicitly requested.
- Do NOT mix Expo Router (frontend/) with React Web (frontend-admin/).
- Do NOT touch files outside the explicitly mentioned scope.

BACKEND:
- ASP.NET Core Controller-based API
- Service layer contains business logic
- EF Core Fluent API, PostgreSQL
- Money logic is critical; no rounding assumptions
- TSE, FinanzOnline, DailyClosing are compliance-critical

FRONTEND (POS):
- React Native + Expo Router
- cash-register.tsx is the main orchestrator screen
- components/ui | components/soft | components/debug

ADMIN WEB:
- Vite + React + TypeScript
- React Router + React Query
- No Expo / RN patterns allowed

OUTPUT RULES:
- Always produce a plan first.
- Then list changed files.
- Then produce file-level patches.
- If assumptions are required, list them explicitly and wait for confirmation.
- No walkthroughs, no self-praise, no "verified" claims.