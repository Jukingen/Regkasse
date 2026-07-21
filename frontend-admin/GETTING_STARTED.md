# Getting Started — Frontend Admin (Day 1)

**Goal:** Run FA locally, log in, and know where the deep docs live.  
**Full guide:** [`ONBOARDING.md`](ONBOARDING.md)  
**Package README:** [`README.md`](README.md)

---

## Before you start

- [ ] Node.js **22+** installed (`node -v`)
- [ ] Repo cloned; you can open `frontend-admin/`
- [ ] Backend can run (`cd backend && dotnet run`) or a teammate shares a reachable API URL
- [ ] Local login credentials from your team (do not commit them)

---

## 30-minute setup

### 1. Install

```bash
cd frontend-admin
npm install
```

### 2. Env file (this folder only)

```bash
cp .env.example .env.local
```

Edit `.env.local`:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
```

> Do **not** put FA `.env.local` at the monorepo root — Next will ignore it for this app.

### 3. Start API + FA

```bash
# Terminal A
cd backend && dotnet run

# Terminal B
cd frontend-admin && npm run dev
```

Open: [http://localhost:3000](http://localhost:3000) (or `http://admin.regkasse.local:3000`).

### 4. Smoke check

- [ ] Login with email **or** username
- [ ] Protected shell loads (sidebar visible)
- [ ] Dev: header tenant switcher works (API must be Development)
- [ ] `/rksv` shows **TEST** (or fix env + `npm run dev:clean` if **UNCONFIGURED**)

### 5. Quality gate once

```bash
npm run typecheck
npm run test -- --reporter=dot
```

---

## Mental model (5 minutes)

| Layer | Job |
| ----- | --- |
| `proxy.ts` | Cookie/JWT present + not expired → else `/login` |
| `AuthGate` + `/me` | Session user |
| `PermissionRouteGuard` | Route permissions → inline 403 |
| TanStack Query / Orval | Server data |
| `useNotify()` | Toasts (never static antd `message`) |

FA calls **`/api/admin/*`** (and Auth). Never call **`/api/pos/*`** from FA.

---

## Your first useful actions

1. Skim [`AGENTS.md`](../AGENTS.md) → Language Rules + API Boundaries + Frontend-Admin conventions.  
2. Read [`ONBOARDING.md`](ONBOARDING.md) §3–4 (architecture + conventions).  
3. Join the next **weekly FA onboarding session** (see ONBOARDING §8).  
4. Pick a **buddy** and a tiny first PR (docs/test/i18n — avoid RKSV/payment until paired).

---

## If something breaks

| Symptom | Quick fix |
| ------- | --------- |
| RKSV **UNCONFIGURED** | Set `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` in `frontend-admin/.env.local` → `npm run dev:clean` |
| Always redirected to `/login` | Clear site data; confirm API up; re-login |
| Empty / wrong tenant data (dev) | Header tenant switcher; API Development mode |
| `npm` / lockfile chaos | Prefer restore `package-lock.json` + `npm ci` |

Full table: [README Troubleshooting](README.md#troubleshooting).

---

## Checklist — Day 1 done

- [ ] FA runs locally  
- [ ] Logged in successfully  
- [ ] Know FA vs POS vs API packages  
- [ ] Bookmarked `ONBOARDING.md` + `AGENTS.md`  
- [ ] Invited to weekly onboarding calendar event  

Welcome aboard.
