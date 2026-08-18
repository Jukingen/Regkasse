# Fiskaly SIGN AT — Production (LIVE) cutover

**Prepared:** 2026-08-18  
**Status:** **Operator procedure only.** This workstation did **not** create a LIVE SCU, store API keys, or sign a LIVE receipt.

Secrets stay in the host secret store (`appsettings.Production.json`, `.env.production`, or systemd `EnvironmentFile`). Never commit keys, PIN, or certificates.

**Related:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · FA `/admin/tse/fiskaly/setup`

---

## What “LIVE” means in this codebase

| Item | Production expectation |
|------|------------------------|
| `Fiskaly:Environment` | `LIVE` (aliases `PROD` / `PRODUCTION`) |
| SIGN AT API host | `https://rksv.fiskaly.com/api/v1` (Austria RKSV). LIVE vs TEST is the **Fiskaly organization + API key pair**, not `api.fiskaly.com`. |
| `Tse:TseMode` / `Tse:Mode` / `Tse:Provider` | `Device` / `Real` / `fiskaly` |
| `Tse:Environment` | `Production` (informational vendor label — **not** `LIVE`; `LIVE` belongs on `Fiskaly:Environment`) |
| `RKSV:Mode` / `RKSV:TseMode` / `RKSV:ShowDemoLabel` | `Production` / `Real` / `false` |
| Config aliases | `Fiskaly:ScuId` = `Fiskaly:TseSerialNumber` = `Fiskaly:SignatureCreationUnitId` |

`Tse:Providers:fiskaly` is merged into `Fiskaly` when dedicated fields are empty. Keep **the same** key, secret, SCU id, and base URL on both blocks.

Template: `backend/appsettings.Production.example.json`. Docker: `.env.production` + `docker-compose.prod.yml`.

---

## Adım 1 — Fiskaly Dashboard: LIVE SCU + API key

Do this in the **LIVE** organization (not the TEST SCU used in Development).

1. Open [https://dashboard.fiskaly.com](https://dashboard.fiskaly.com).
2. Select the organization → **SIGN AT** (Austria / RKSV).
3. Switch environment to **LIVE** (not TEST).
4. Create a **LIVE** API key + secret. Store them in the host secret store (`Fiskaly:ApiKey` / `Fiskaly:ApiSecret`, or `FISKALY_API_KEY` / `FISKALY_API_SECRET`).
5. Create the production **Signature Creation Unit (SCU)**. Record the SCU UUID (`Fiskaly:ScuId` / `FISKALY_SCU_ID`).
6. Export / store the leaf **signing certificate** (DER Base64) for DEP (`Fiskaly:SigningCertificateDerBase64` / `FISKALY_SIGNING_CERT_DER_B64`).
7. Store the FON-registered AES-256 turnover key (`Fiskaly:TurnoverCounterAesKeyBase64` / `FISKALY_TURNOVER_AES_KEY_B64`) if not already on the SCU.

Host config (do not paste secrets into git):

```json
"Fiskaly": {
  "Enabled": true,
  "Environment": "LIVE",
  "ApiBaseUrl": "https://rksv.fiskaly.com/api/v1",
  "ApiKey": "<LIVE key from Dashboard>",
  "ApiSecret": "<LIVE secret>",
  "ScuId": "<LIVE SCU UUID>",
  "SigningCertificateDerBase64": "<leaf DER>",
  "TurnoverCounterAesKeyBase64": "<32-byte AES, Base64>"
}
```

Also set `Tse:Providers:fiskaly` to the same values. Env form: `Fiskaly__ApiKey`, `Fiskaly__ScuId`, `FISKALY_SCU_ID`, …

Restart the API, then verify as Super Admin (permission `cashregister.view`):

```http
GET /api/admin/fiskaly/status
```

Expect:

| Field | After Adım 1 |
|-------|----------------|
| `environment` | `LIVE` |
| `isEnabled` / `isAuthenticated` | `true` |
| `scuId` | LIVE SCU UUID |
| `scuState` | `CREATED` (not yet FON-initialized) |
| `scuInitialized` | `false` |

If `environment` is still `TEST`, `Fiskaly:Environment` was not set (Docker previously defaulted the C# property to TEST even on the SIGN AT host).

---

## Adım 2 — FON ile SCU initialize

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

Expect `scuState` / `scu.state` = `INITIALIZED` and `scuInitialized` = `true`. Cash registers on setup should be `INITIALIZED`.

### B) FinanzOnline SOAP (outbox / RKSV submission)

Host config / `company_settings`: username + password + `TelematikId` + `HerstellerId`.  
`FinanzOnline:RksvSubmission:ClientKind=Real`, all `UseSimulation=false`. Details: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md).

---

## Adım 3 — Production fiscal config (DEMO label off)

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

## Adım 4 — UID ve firma bilgileri

POS receipts read **tenant `company_settings`**, not `appsettings` `Company`. `ATU00000000` is the `CompanyProfile` fallback default — replace it on the mandant.

| Surface | Where |
|---------|--------|
| Mandanten-Admin | FA `/settings/company` (`companyTaxNumber` must match `^ATU\d{8}$`) |
| Super Admin | FA `/admin/tenants/{id}/settings` |
| Some reports (Tagesabschluss composer) | optional host `Company:*` in untracked `appsettings.Production.json` |

Do **not** put a real Handelsregister UID into git. Placeholder only in `appsettings.Production.example.json`.

Verify on a later receipt: Firmenname, Adresse, UID (`ATU` + 8 digits). Not `ATU00000000`.

---

## Adım 5 — Production receipt (ComplianceOfficer)

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

Keep TEST keys only on Development/Staging. Rolling Production back to TEST SCU or Soft TSE is a **No-Go** for paying mandants unless ComplianceOfficer documents an Ausfall path.
