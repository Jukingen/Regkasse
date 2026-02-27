# Regkasse Monorepo Map

This document is the single source of truth for AI agents (Codex, Antigravity, etc.)
to correctly understand the structure and architectural boundaries of the Regkasse repository.

The repository consists of three independent applications:

1) Backend (C# / .NET / EF Core)
2) Mobile POS (Expo Router)
3) Admin Panel (Next.js 14)

---

# 1. Repository Structure

> Update folder names below if necessary to match the actual repo structure.

## Backend
- Path: `./backend`
- Tech: C# .NET Core
- ORM: EF Core + Fluent API
- Database: PostgreSQL
- Primary Keys: string and Guid (mixed usage)
- Domain: Transaction-heavy POS system
- Domain Flow: Cart → Order → Receipt → DailyClosing
- Rule: EF Core shadow properties are NOT used.

## Mobile POS
- Path: `./frontend` (or `./mobile`)
- Tech: Expo Router (React Native)
- Main Screen: `cash-register.tsx`
- Architecture: Large orchestrator screen + small UI components
- UI folders:
  - `components/ui`
  - `components/soft`
  - `components/debug`

## Admin Panel
- Path: `./frontend-admin` (or `./admin`)
- Tech: Next.js 14 (App Router), TypeScript, Ant Design
- Server State: React Query
- Client Global State: Zustand
- API Layer: Axios + auto-generated Orval hooks
- Strict Rule:
  - Admin MUST NEVER use Expo or React Native patterns.

---

# 2. Architectural Hard Rules

## 2.1 Cross-Application Boundaries

- Mobile must not use Next.js or AntD web-only patterns.
- Admin must not use React Native / Expo patterns (e.g. StyleSheet, RN navigation patterns).
- Backend domain logic must remain isolated from UI concerns.

## 2.2 POS Domain Invariants

The core POS flow is:

Cart → Order → Receipt → DailyClosing

Critical requirements:
- Transaction consistency
- Idempotency (no duplicate receipts or payments)
- Daily closing integrity
- Proper concurrency handling

## 2.3 EF Core Constraints

- Fluent API configuration is preferred.
- Shadow properties are intentionally avoided.
- Mixed string + Guid PK usage requires careful indexing and FK consistency.

---

# 3. Commands Map

Agents must verify commands by reading project files before assuming.

## Backend
- Çalışma dizini: `backend/`
- Build:
  ```bash
  cd backend && dotnet build
  ```
- Test:
  ```bash
  cd backend && dotnet test
  ```
- Run:
  ```bash
  cd backend && dotnet run
  ```
  (API: http://localhost:5183, Swagger: http://localhost:5183/swagger)
- Migrations (if enabled):
  ```bash
  cd backend && dotnet ef migrations list
  cd backend && dotnet ef database update
  ```

## Mobile
- Install:
  npm install / pnpm install / yarn
- Start:
  npx expo start
- Lint/Test:
  npm run lint
  npm test

## Admin
- Install:
  npm install / pnpm install / yarn
- Dev:
  npm run dev
- Build:
  npm run build
- Lint/Test:
  npm run lint
  npm test
- Orval (if configured):
  npm run generate:api (or equivalent script)

---

# 4. AI Agent Operating Mode

## Codex (Analysis Mode)
Purpose:
- Architecture analysis
- Risk identification
- Prioritized backlog (P0 / P1 / P2)
- Generate Antigravity master prompts

Restrictions:
- No file modifications
- No patches/diffs
- No git commits or PRs

## Antigravity (Implementation Mode)
Work should be divided into 3 parallel streams:
1. Backend
2. Mobile
3. Admin

Each task must include:
- Clear acceptance criteria
- Test strategy
- Risk analysis
- Rollback strategy (if high-risk)

---

# 5. Initial Agent Checklist

Before generating plans, agents must:

1. Confirm the three root applications exist.
2. Detect .NET SDK version from global.json (if present).
3. Detect test frameworks from *.csproj.
4. Inspect package.json scripts for Mobile and Admin.
5. Verify Orval configuration (Admin).
6. Produce:
   - Repository Map
   - Commands Map
   - Risk Analysis
   - Prioritized Backlog
   - Antigravity Master Prompt