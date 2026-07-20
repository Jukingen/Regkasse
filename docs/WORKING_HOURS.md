# Working Hours — Scope & Non-Gating Contract

> **Core rule:** Working hours restrict **customer-facing digital order intake only** (tenant websites / apps).  
> They **never** block POS, Frontend-Admin (FA), or authenticated POS/admin API operations.

**Last verified:** 2026-07-19  
**Always-applied summary:** [`AGENTS.md`](../AGENTS.md) § Working hours (website/app only)  
**Related:** [`DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) · [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md)

---

## Applies ONLY to

| Surface | Behavior |
|---------|----------|
| Tenant websites (`frontend-sites`) | UI disables order CTAs when `canOrder=false`; shows open/closed |
| Tenant mobile apps (PWA / native) | Same status API + intake gate |
| Customer-facing digital services | Online order placement blocked outside hours / after cutoff |

## NEVER applies to

| Surface | Behavior |
|---------|----------|
| POS FE (`frontend`) | Always operational — display / Tagesabschluss reminders only |
| FA (`frontend-admin`) | Full management of schedule; never access-gated by open/closed |
| Authenticated POS / admin APIs | Cart, payment, receipts, settings CRUD — no hours evaluation |

**Nuance:** Anonymous public intake `POST /api/public/online-orders` **is** gated (website/app only). That is intentional and does **not** mean “backend API closed”.

---

## Status vs intake

| Field | Meaning | Used by |
|-------|---------|---------|
| `isOpen` | Restaurant local open window | Website display |
| `canOrder` | Intake allowed (`isOpen` **and** before `stopOnlineOrdersMinutesBeforeClose`) | Website/app order CTAs + public place-order |
| POS `restaurantIsOpen` / `isOpen` | Display alias of schedule window | POS banner / reminder only |
| POS `posOperationsAllowed` | **Always `true`** | Never use as a gate |

---

## Implementation map (verified)

### Website status (display)

**File:** `backend/Sites/Controllers/WebsiteStatusController.cs`

- Route: `GET /api/sites/{tenantSlug}/status` (+ `/special`)
- Anonymous; slug-scoped
- Called by customer websites / PWAs / apps only
- Returns open / can-order / message — **does not affect POS or FA**

### Online order intake (server gate)

| Piece | Role |
|-------|------|
| `OnlineOrderIntakeService` | Calls `WorkingHoursSettings.EvaluateWebsiteStatus`; rejects with `ONLINE_ORDERS_CLOSED` (HTTP 409) when `!CanOrder` |
| `PublicOnlineOrdersController` | Public place-order surface only |
| `WorkingHoursSettings.IsAcceptingOnlineOrders` / `EvaluateWebsiteStatus` | Documented: never apply to POS cart / payment |

### POS (non-gating)

| Piece | Role |
|-------|------|
| `frontend/app/(tabs)/cash-register.tsx` | Main POS screen (`CashRegisterScreen`) — **no** hours check; always accepts orders |
| `frontend/hooks/useWorkingHours.ts` | Forces `posOperationsAllowed: true` |
| `frontend/utils/workingHoursStatus.ts` | Display / reminder helpers only |
| `WorkingHoursStatus` / Tagesabschluss reminder | Informational UI — does not disable cart/payment |

> Note: There is no `frontend/app/(tabs)/index.tsx` cash-register route. The tab entry is `cash-register.tsx`.

### FA (management only)

| Piece | Role |
|-------|------|
| `/settings/working-hours` | CRUD for schedule / special days / online cutoff |
| `WorkingHoursSettingsForm` | Shows `settings.workingHours.protectionNote` |
| Settings APIs | Persist JSON on `CompanySettings.WorkingHours` — no intake evaluation |

---

## Contract tests

| Suite | Asserts |
|-------|---------|
| `WorkingHoursPosFaNonGatingContractTests` | POS/admin controllers do not call `IsAcceptingOnlineOrders` / `EvaluateWebsiteStatus` / `GetWebsiteStatusAsync` outside allowed public surfaces |
| `frontend/__tests__/workingHoursStatus.test.ts` | POS never gates on `restaurantIsOpen`; hook always forces `posOperationsAllowed` |
| `frontend-admin/.../workingHoursNonGating.contract.test.ts` | FA form/page/API are management-only |

```bash
cd backend && dotnet test --filter "FullyQualifiedName~WorkingHours"
cd frontend && npm run test -- --run workingHoursStatus
cd frontend-admin && npm run test -- --run workingHoursNonGating
```

---

## Do NOT

- Do not call `EvaluateWebsiteStatus` / `IsAcceptingOnlineOrders` from POS payment, cart, or register controllers
- Do not disable POS cart / payment / login based on restaurant hours
- Do not block FA routes or admin APIs when the restaurant is “closed”
- Do not treat `autoClosePOSAtClosing` as automatic Tagesabschluss (preference / reminder only)
