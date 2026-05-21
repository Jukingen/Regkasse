# Cash register lifecycle (RKSV)

> **Audience:** Tenant admins, Super Admin support, compliance reviewers.  
> **UI:** German (de-AT). **Technical:** English.

Describes cash register **statuses**, **RKSV-compliant decommission** (Schlussbeleg / Endbeleg), why **hard delete is not allowed** in production, and audit expectations.

Related: [`TENANT_MANAGEMENT.md`](TENANT_MANAGEMENT.md), [`MULTI_TENANT.md`](MULTI_TENANT.md), RKSV special receipts in backend `RksvSpecialReceiptService`.

---

## Register statuses

| Status (EN) | German (FA) | Meaning |
|-------------|-------------|---------|
| `Closed` | Geschlossen | No open shift; payments allowed when register is active and licensed |
| `Open` | Offen | Active shift — must be **closed** before Schlussbeleg / decommission |
| `Maintenance` | Wartung | Operational restriction (tenant policy) |
| `Disabled` | Deaktiviert | Administratively disabled |
| `Decommissioned` | Stillgelegt | **Final** — RKSV Schlussbeleg issued; **no new payments** |

**Enum:** `RegisterStatus` — `backend/Models/`.

POS and API reject payments on decommissioned registers with `RKSV_REGISTER_DECOMMISSIONED` / `CASH_REGISTER_DECOMMISSIONED_RKSV`.

---

## Why hard delete is NOT allowed (production)

| Requirement | Implementation |
|-------------|----------------|
| **RKSV §7** — Belegdaten müssen aufbewahrt werden | Receipts / `payment_details` remain in DB; register row kept with `Decommissioned` |
| **Audit trail** | `AuditLogActions.CASH_REGISTER_DECOMMISSION` with Schlussbeleg receipt number |
| **One Schlussbeleg per register** | Unique index on active `Schlussbeleg` per `cash_register_id` |

**Production / normal operation:** operators use **Stilllegen** (decommission), not delete.

### Development-only hard delete

- Endpoint: `DELETE /api/admin/cash-registers/{id}` (guarded)
- Allowed only when `ASPNETCORE_ENVIRONMENT=Development` **and** `CashRegister:AllowHardDelete=true`
- Requires confirm phrase `HARD_DELETE` and **zero** payment rows on the register
- Audit: `CASH_REGISTER_HARD_DELETE` with note *„NUR FÜR TESTUMGEBUNGEN“*

---

## Schlussbeleg (Endbeleg) requirement

Permanent decommission **always** creates an RKSV **Schlussbeleg** (zero-total Endbeleg) first, then sets register status to `Decommissioned`.

### Preconditions

| Rule | German operator message |
|------|-------------------------|
| Register must exist in **current tenant** | — |
| **No open shift** | *„Die Kasse muss geschlossen sein (keine offene Schicht) …“* |
| Status must be **Closed** | Same as above in API |
| No duplicate Schlussbeleg | 409 if already decommissioned |

**Service flow:** `CashRegisterDecommissionService.DecommissionAsync` → `RksvSpecialReceiptService.CreateSchlussbelegAsync` → updates `decommissioned_at_utc`, `decommission_reason`, `Status = Decommissioned`.

Alternative manual path (operators with permission):

- FA: **Sonderbelege / Schlussbeleg** (`RksvSpecialReceiptsController`)
- Permission: `AppPermissions.RksvSchlussbelegCreate`

---

## How to decommission (Frontend Admin)

**Route:** `/kassenverwaltung` — `frontend-admin/src/app/(protected)/kassenverwaltung/page.tsx`  
**Action:** **Stilllegen** on a closed register → `DecommissionModal`

![Decommission modal (placeholder)](images/cash-registers/fa-decommission-modal.png)

### Modal warnings (German)

| Warning | Purpose |
|---------|---------|
| Keine neuen Zahlungen mehr | Business stop |
| Belege 7 Jahre gespeichert | Retention (RKSV / DSGVO operator copy) |
| Kasse kann NICHT wiederhergestellt werden | Irreversible |
| Info: Schlussbeleg wird automatisch erstellt | Explains backend flow |

Optional **Grund** field → stored as `decommission_reason`.

**Confirm:** *„Trotzdem stilllegen“* — calls:

```http
PUT /api/admin/cash-registers/{id}/decommission
Content-Type: application/json

{ "reason": "Gerät defekt / Ersatz" }
```

**Success (DE):** *„Schlussbeleg erstellt — Kasse stillgelegt. Keine neuen Zahlungen erlaubt.“*

### List filter

- Default: decommissioned registers **hidden**
- Toggle: *„Stillgelegte Kassen anzeigen“* (`showDecommissioned`)

Tenant detail tab: `TenantDetailCashRegistersTab` (Super Admin) uses the same decommission API in tenant context (after impersonation or dev header).

---

## Audit log entries

On successful decommission (`TryAuditDecommissionAsync`):

| Field | Value |
|-------|--------|
| **Action** | `CASH_REGISTER_DECOMMISSION` |
| **Entity** | `CashRegister` / `{id}` |
| **oldValues** | `{ status, registerNumber }` |
| **newValues** | `{ status: Decommissioned, registerNumber, receiptNumber, paymentId, receiptId, decommissionReason }` |
| **description** | `Cash register {registerNumber} decommissioned via RKSV Schlussbeleg (Endbeleg).` |

Audit write failures are logged as warnings; decommission itself still completes.

---

## API reference

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| `PUT` | `/api/admin/cash-registers/{id}/decommission` | Tenant admin permissions | Schlussbeleg + status |
| `POST` | `/api/rksv/special-receipts/schlussbeleg` | `RksvSchlussbelegCreate` | Lower-level; same core service |
| `DELETE` | `/api/admin/cash-registers/{id}` | Dev + `AllowHardDelete` only | Empty test registers |

**Controller:** `backend/Controllers/AdminCashRegistersController.cs`  
**Service:** `backend/Services/AdminCashRegisters/CashRegisterDecommissionService.cs`

---

## POS behavior

After decommission, POS readiness shows German hard-stop copy (`posRegisterGateCopy.ts`):

- Register status / resolution `decommissioned` blocks checkout
- Error code alignment: `RKSV_REGISTER_DECOMMISSIONED`

---

## Key files

| Area | Path |
|------|------|
| FA page | `frontend-admin/src/app/(protected)/kassenverwaltung/page.tsx` |
| Modal | `frontend-admin/src/features/cash-registers/components/DecommissionModal.tsx` |
| API client | `frontend-admin/src/features/cash-registers/api/cashRegisters.ts` |
| Status helpers | `frontend-admin/src/features/cash-registers/utils/registerStatus.ts` |
| RKSV service | `backend/Services/RksvSpecialReceiptService.cs` (`CreateSchlussbelegAsync`) |
| i18n (DE) | `frontend-admin/src/i18n/locales/de/cashRegisters.json` |
| Tests | `backend/KasseAPI_Final.Tests/RksvSchlussbelegServiceTests.cs`, `CashRegisterDecommissionServiceTests.cs` |

---

## Screenshots

Place assets under `docs/images/cash-registers/` (see README in that folder if present).
