# POS shift open — business policy

**Status:** Decision record. Not implemented.  
**Last updated:** 2026-09-21  
**Default in code today:** The Cashier role includes `shift.open`. Selecting a closed register on POS calls auto-open.  
**Not in code today:** A per-tenant switch that removes `shift.open` from Cashier. That work is Paket 31 (see [TODO](#todo--paket-31)).

This document chooses when a mandant lets cashiers open a closed register themselves, and when only a Mandanten-Admin (`Manager`) may open it. It does not change `RolePermissionMatrix`.

Related surfaces:

- Role grants: `backend/Authorization/RolePermissionMatrix.cs` (`Roles.Cashier`, `Roles.Manager`)
- POS picker: `frontend/app/(screens)/cash-register-select.tsx`
- Auto-open: `POST /api/pos/shift/auto-open` (`shift.open`)
- Open request (Paket 30-c): `POST /api/pos/cash-register/open-requests` (`cart.view`), `GET /api/pos/cash-register/open-requests/mine`
- FA queue: `GET /api/admin/cash-registers/open-requests`, approve and deny (`cash_register.manage`), `CashRegisterOpenRequestsPanel` on `/kassenverwaltung`

---

## Current default

| Actor | `shift.open` | What happens on “Kasse wählen” |
|-------|----------------|--------------------------------|
| Cashier | Yes (role matrix) | Closed register is selectable. POS calls `POST /api/pos/shift/auto-open`. |
| Mandanten-Admin (`Manager`) | Yes | Same auto-open path. |
| Cashier without `shift.open` | No | Closed row is not auto-opened. POS offers „Kassenöffnung anfordern“ (`POST /api/pos/cash-register/open-requests`). |

The POS filter for `app_context=pos` does not strip `shift.open` from Cashier. The admin JWT strip (`AdminAppPermissionProfile`) applies to FA sessions, not to POS login.

There is no `CompanySettings` flag and no other tenant setting that turns Cashier `shift.open` off. Every mandant gets the Cashier grant.

`REGISTER_UNAVAILABLE` from auto-open is a separate failure (inactive register, assignment, occupancy, Startbeleg, Monatsbeleg, and similar). It is not this policy. A cashier who already has `shift.open` can still see that error.

---

## Alternative: Manager-only open

Some businesses want only the Mandanten-Admin to open a register. Cashiers then use the existing open-request flow (Paket 30-c):

1. Cashier selects a closed register and sends an open request (`cart.view`).
2. POS polls `GET /api/pos/cash-register/open-requests/mine`.
3. Mandanten-Admin approves or denies on FA (`cash_register.manage`).
4. Approve opens the register for the requesting cashier (`CashRegisterOpenRequestService`).

Manager and Super Admin keep `shift.open`. Their POS session still auto-opens. This alternative does not remove `shift.open` from the Manager row in the role matrix.

---

## Decision matrix

Enable **cashier auto-open** (today’s default) when all of the following are true:

| Signal | Why it matters |
|--------|----------------|
| Cashiers are trusted to start the fiscal day | Opening a register starts the operational shift and can require TSE signing downstream. |
| No Mandanten-Admin is on site at open | Waiting for an approval queue blocks the first sale. |
| One shared register, or each cashier is assigned their own | Auto-open matches “I pick my till and start.” |
| Startbeleg already exists and Monatsbeleg is not blocking sales | Otherwise auto-open fails even with `shift.open`. |

Disable **cashier auto-open** (Manager-only) when any of the following are true:

| Signal | Why it matters |
|--------|----------------|
| House rule: only the Mandanten-Admin opens the till | Cashier must not change register status alone. |
| Shared tills and strict assignment | A cashier must not open a register assigned to someone else. Assignment already rejects auto-open; the request queue is the supported ask. |
| Opening balance or the first receipt must be supervised | Approval is the control point. |
| The mandant staffs a back office that watches `/kassenverwaltung` | The FA queue is useful only if someone reads it. |

Do not disable cashier auto-open to “fix” `REGISTER_UNAVAILABLE`. That code is a register or RKSV gate, not a missing permission.

---

## Impact on POS UX

| Policy | Closed register on „Kasse wählen“ | Copy the cashier sees |
|--------|-----------------------------------|------------------------|
| Cashier has `shift.open` | Select runs auto-open, then the sale screen. | „Geschlossen — wird beim Auswählen geöffnet“ |
| Cashier lacks `shift.open` | Select does not auto-open. The row offers a request. | „Kassenöffnung anfordern“ / „Kasse muss von Ihrem Mandanten-Admin geöffnet werden.“ |
| HTTP 403 on auto-open | Picker shows the manager-open hint. It does not show the `REGISTER_UNAVAILABLE` sentence. | „Kasse muss von Ihrem Mandanten-Admin geöffnet werden.“ |

A future confirm step („Öffnung beim Manager anfordern?“) belongs to the request path. It is not built as a modal today. The request control is an inline button on the closed row.

Manager on POS keeps the auto-open row. Do not send Mandanten-Admin through the request queue.

---

## Impact on FA

| Policy | `/kassenverwaltung` queue |
|--------|---------------------------|
| Cashier auto-open (default) | Queue stays empty for normal opens. Cashiers never call `POST /api/pos/cash-register/open-requests`. |
| Manager-only | Pending rows appear for cashiers who request an open. Approve opens the register as that cashier. Deny leaves it closed. |

The panel is already mounted when the user has `cash_register.manage`. Mandanten-Admin has that permission. A cashier FA session does not.

Someone with `cash_register.manage` must watch the queue. If nobody approves, the cashier stays on „Kasse wählen“ with a pending request. POS polls that list about every 10 seconds while a request is pending or a closed register is visible without `shift.open`.

---

## TODO — Paket 31

Do not edit `RolePermissionMatrix` for this decision. Cashier keeps `shift.open` in the matrix so the default stays auto-open.

Paket 31 adds a runtime overlay:

| Item | Intended shape |
|------|----------------|
| Setting | `CompanySettings.AllowCashierShiftOpen` (`bool`, default `false` only if product chooses Manager-only as the new default; **today the effective default is allow**, because the matrix already grants `shift.open`) |
| Storage | Additive column on `company_settings`. No second permission key. |
| Overlay | POS Cashier: if the flag is off, drop `shift.open` for that request and for `GET /api/Auth/me`. If the flag is on, keep `shift.open`. |
| Unchanged | Manager `shift.open`. FA admin JWT strip. Open-request routes (`cart.view` / `cash_register.manage`). |
| UX | Flag off → request path on „Kasse wählen“. Flag on → current auto-open. |
| Cache | Invalidate `tenant_settings_{tenantId}` when company settings change so the next POS call sees the new flag. |

**Product choice still open:** whether new mandants default to allow (match today) or to Manager-only (flag default `false` plus overlay that strips the matrix grant). Write that default into the Paket 31 migration only after this choice is signed off. Until then, behavior stays “Cashier has `shift.open`.”
