# Fiskaly SIGN AT — Production (LIVE) cutover

**Last updated:** 2026-11-12  
**Status:** Operator procedure. Each mandant brings its own Fiskaly organization (BYOK). There is no shared production organization.

API key and secret are stored as Data Protection ciphertext on `tenant_fiskaly_credentials` (one row per tenant, unique `tenant_id`). The API returns `apiKeyHint` only. Never commit keys, PIN, or certificates.

**Related:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · FA `/settings` (credentials) · FA `/admin/tse/fiskaly/setup` (FON / SCU)

---

## What “LIVE” means in this codebase

| Item | Production expectation |
|------|------------------------|
| `Fiskaly:Environment` | Host default only. A tenant row stores its own `TEST` or `LIVE`. |
| SIGN AT API host | `https://rksv.fiskaly.com/api/v1` (Austria RKSV). LIVE vs TEST is that tenant's Fiskaly organization and API key pair, not `api.fiskaly.com`. |
| `Tse:TseMode` / `Tse:Mode` / `Tse:Provider` | `Device` / `Real` / `fiskaly` |
| `Tse:Environment` | `Production` (informational vendor label — **not** `LIVE`). `LIVE` is the tenant row's `environment`, or host `Fiskaly:Environment` in Development only. |
| `RKSV:Mode` / `RKSV:TseMode` / `RKSV:ShowDemoLabel` | `Production` / `Real` / `false` |
| Config aliases | `Fiskaly:ScuId` = `Fiskaly:TseSerialNumber` = `Fiskaly:SignatureCreationUnitId` |

`Tse:Providers:fiskaly` still merges into host `Fiskaly` when those host fields are empty. Production signing does **not** read that host key pair. `FiskalyTenantCredentialResolver` uses `tenant_fiskaly_credentials` for the ambient tenant. Host `Fiskaly:ApiKey` / `ApiSecret` / `ScuId` apply only in Development, and only when that tenant has no credential row. An inactive row fails closed (no host fallback).

Template for non-secret host flags: `backend/appsettings.Production.example.json`. Docker: `.env.production` + `docker-compose.prod.yml`.

---

## BYOK — one Fiskaly organization per mandant

The Mandanten-Admin owns the Fiskaly account. Regkasse does not share one organization or one SCU across tenants.

| Item | Where |
|------|--------|
| Who enters it | Mandanten-Admin with `cash_register.manage` |
| FA | `/settings` — API key, API secret, SCU UUID, environment `TEST` or `LIVE` |
| API | `GET` / `PUT /api/admin/fiskaly/credentials` |
| Storage | `tenant_fiskaly_credentials.api_key_protected`, `api_secret_protected`, `scu_id`, `environment` |
| Shown back | Last characters of the key (`api_key_hint`). The secret is write-only; send it again only to rotate it. |
| Cache | In-memory, about 5 minutes. A successful save clears that tenant's cache and access token. |

`PUT` requires a UUID SCU id and environment `TEST` or `LIVE`. The first save requires both key and secret. A later save may omit the secret to keep the stored ciphertext.

The same row has `signing_certificate_der_base64` (leaf DER, public). The FA form does not write it. When the credential source is the tenant row, signing and DEP require that column. Host `Fiskaly:SigningCertificateDerBase64` is not a substitute once the tenant row is active.

---

## Step 1 — That mandant's LIVE SCU + API key

Do this in **that mandant's** LIVE organization.

1. The mandant opens [https://dashboard.fiskaly.com](https://dashboard.fiskaly.com) on their own Fiskaly account.
2. Select their organization → **SIGN AT** (Austria / RKSV).
3. Switch that organization to **LIVE**.
4. Create a LIVE API key and secret. Do not put them in `appsettings`, `.env`, or git.
5. Create the production SCU and copy the SCU UUID.
6. Export the leaf signing certificate (DER Base64). It belongs on `tenant_fiskaly_credentials.signing_certificate_der_base64` for this tenant. The credential form does not collect it.
7. Keep the FON-registered AES turnover key on the SCU (or the host secret store if Fiskaly still requires `Fiskaly:TurnoverCounterAesKeyBase64` for that deployment). Do not commit it.

In FA, as the Mandanten-Admin of that tenant (`/settings`):

1. Set environment to `LIVE`.
2. Paste API key, API secret, and SCU UUID.
3. Save.

Verify (ambient tenant must be that mandant; permission `cash_register.manage`):

```http
GET /api/admin/fiskaly/credentials
```

Expect `configured: true`, `environment: LIVE`, a non-empty `scuId`, and an `apiKeyHint`. The response has no secret. That endpoint is the BYOK check.

`GET /api/admin/fiskaly/status` (permission `cash_register.view`) still fills `environment`, `isConfigured`, and the SCU probe from host `Fiskaly` options. It does not echo the tenant row. Signing calls use the tenant key when the row is active. Do not treat a host-empty `scuId` on this status payload as “credentials missing” if `GET /api/admin/fiskaly/credentials` is configured.

If Production credentials return `configured: false`, the tenant row is missing or inactive. Setting `Fiskaly:ApiKey` on the host does not fix it.

---

## Step 2 — Initialize the SCU with FON

Fiskaly SCU setup and BMF SOAP are **not** the same secret.

### A) Fiskaly FON authentication (SCU gate)

Prerequisite: at least one **non-decommissioned** cash register exists for the ambient mandant.

FA Super Admin (`system.critical`): `/admin/tse/fiskaly/setup`

Wizard order (enforced by `FiskalySetupService`):

1. Enter production FON credentials (PIN is never stored or logged):

   | Fiskaly field | BMF name |
   |---------------|----------|
   | `FonParticipantId` | Teilnehmer-ID (`tid`) — 8–12 alnum |
   | `FonUserId` | Benutzer-ID (`benid`) |
   | `FonUserPin` | PIN |

2. **FON Authenticate** → `POST /api/admin/fiskaly/fon/authenticate`
3. **SCU Initialize** → `POST /api/admin/fiskaly/scu/initialize` (requires FON authenticated; PATCH SCU → `INITIALIZED`)
4. **Cash Register Initialize** → `POST /api/admin/fiskaly/cash-register/{id}/initialize` (`CREATED` → `REGISTERED` → `INITIALIZED`)

Confirm FON registration of Kassen-ID + SCU at BMF, not only Fiskaly UI.

Verify:

```http
GET /api/admin/fiskaly/status
GET /api/admin/fiskaly/setup
```

`GET /api/admin/fiskaly/setup` is the initialization snapshot for the ambient tenant. Its SCU id comes from host `Fiskaly:ScuId`, else tenant setting `Fiskaly:ScuId`, else the active fiskaly `tse_devices` id. It does not read `tenant_fiskaly_credentials.scu_id`. Signing does. Use the same SCU UUID in both places.

Expect setup `scu.state` = `INITIALIZED`. `GET /api/admin/fiskaly/status` sets `scuInitialized` only when host `Fiskaly:ScuId` is set. Cash registers on setup should be `INITIALIZED`.

### B) FinanzOnline SOAP (outbox / RKSV submission)

Host config / `company_settings`: username + password + `TelematikId` + `HerstellerId`.  
`FinanzOnline:RksvSubmission:ClientKind=Real`, all `UseSimulation=false`. Details: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md).

---

## Step 3 — Production fiscal config (DEMO label off)

`backend/appsettings.Production.example.json` and `docker-compose.prod.yml` already ship these defaults. Copy them into the **untracked** host file / `.env.production`; do not commit secrets.

```json
"RKSV": {
  "Mode": "Production",
  "TseMode": "Real",
  "FinanzOnlineMode": "Real",
  "ShowDemoLabel": false
},
"Tse": {
  "TseMode": "Device",
  "Mode": "Real",
  "Environment": "Production",
  "Provider": "fiskaly"
}
```

After restart, Production fails closed if TSE is Soft/Demo/Fake or FON is still Simulation (`TseProductionOptionsValidator`, `ProductionRuntimeConfigurationGuard`).

Verify DEMO label is off:

```http
GET /api/rksv/environment
GET /health/tse/mode
GET /api/health/ready
```

Expect `showDemoLabel: false`, `isDemoMode: false`, TSE lock Production / Device. Receipt footer becomes `RKSV-konform (Registrierkassensicherheitsverordnung)` — not `DEMO / NICHT FISKAL`.

---

## Step 4 — UID and company details

POS receipts read **tenant `company_settings`**, not `appsettings` `Company`. `ATU00000000` is the `CompanyProfile` fallback default — replace it on the mandant.

| Surface | Where |
|---------|--------|
| Mandanten-Admin | FA `/settings/company` (`companyTaxNumber` must match `^ATU\d{8}$`) |
| Super Admin | FA `/admin/tenants/{id}/settings` |
| Some reports (Tagesabschluss composer) | optional host `Company:*` in untracked `appsettings.Production.json` |

Do **not** put a real Handelsregister UID into git. Placeholder only in `appsettings.Production.example.json`.

Verify on a later receipt: Firmenname, Adresse, UID (`ATU` + 8 digits). Not `ATU00000000`.

---

## Step 5 — Production receipt (ComplianceOfficer)

A LIVE “test payment” is a **real RKSV receipt**. Do **not** run POS `SMOKE_POS_PAYMENT=1` against Production.

Preferred order:

1. `GET /api/health/ready` and `GET /health/tse/mode` — fiscal posture Production / Device.
2. FA Fiskaly status: FON authenticated, SCU `INITIALIZED`.
3. RKSV special receipt allowed by ComplianceOfficer (**Startbeleg** / **Nullbeleg**), not an arbitrary sale.
4. On the printed receipt:
   - No `DEMO / NICHT FISKAL`
   - TSE line is `TSE-Signatur:` plus compact JWS (not `TSE-Signatur: nicht verfügbar`)
   - RKSV QR present (`_R1-AT1_…`)
   - Correct company UID
5. Confirm compact JWS + certificate thumbprint on the payment row; DEP export for that register.
6. Optional: scan QR with BMF **Belegcheck**.

Until that evidence exists, GO_LIVE C2 remains open.

---

## Rollback

Keep another mandant's TEST keys off this tenant's row. Development may still use host `Fiskaly:*` keys when the tenant has no row. Rolling a paying mandant back to a TEST SCU or Soft TSE is a **No-Go** unless ComplianceOfficer documents an Ausfall path. Deactivate or replace that tenant's row; do not point every mandant at one host key.
