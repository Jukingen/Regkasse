# Fiskaly SIGN AT — Production (LIVE) cutover

**Prepared:** 2026-08-17  
**Status:** **Operator procedure only.** This workstation did **not** create a LIVE SCU, store API keys, or sign a LIVE receipt.

Secrets stay in the host secret store (`appsettings.Production.json` or systemd `EnvironmentFile`). Never commit keys, PIN, or certificates.

**Related:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · FA `/admin/tse/fiskaly/setup`

---

## What “LIVE” means in this codebase

| Item | Production expectation |
|------|------------------------|
| `Fiskaly:Environment` | `LIVE` (aliases `PROD` / `PRODUCTION`) |
| SIGN AT API host | `https://rksv.fiskaly.com/api/v1` (Austria RKSV). LIVE vs TEST is the **Fiskaly organization + API key pair**, not `api.fiskaly.com`. |
| `Tse:TseMode` / `Tse:Mode` / `Tse:Provider` | `Device` / `Real` / `fiskaly` |
| `RKSV:Mode` / `RKSV:TseMode` | `Production` / `Real` |
| Config aliases | `Fiskaly:ScuId` = `Fiskaly:TseSerialNumber` = `Fiskaly:SignatureCreationUnitId` |

`Tse:Providers:fiskaly` is merged into `Fiskaly` when dedicated fields are empty. Keep **the same** key, secret, SCU id, and base URL on both blocks.

Template: `backend/appsettings.Production.example.json`.

---

## 1. Fiskaly Dashboard (human)

Do this in the **LIVE** organization (not the TEST SCU used in Development).

1. Create or select the **LIVE** API key + secret.
2. Create the production **Signature Creation Unit (SCU)**.
3. Record the SCU UUID (`ScuId`).
4. Export / store the leaf **signing certificate** (DER Base64) for DEP (`Fiskaly:SigningCertificateDerBase64`).
5. Store the FON-registered AES-256 turnover key (`Fiskaly:TurnoverCounterAesKeyBase64`) if not already on the SCU.

---

## 2. FON credentials (two different stores)

Fiskaly SCU setup and BMF SOAP are **not** the same secret.

### A) Fiskaly FON authentication (SCU gate)

FA Super Admin: `/admin/tse/fiskaly/setup` → authenticate FON, then initialize SCU, then initialize each cash register.

| Fiskaly field | BMF name |
|---------------|----------|
| `FonParticipantId` | Teilnehmer-ID (`tid`) |
| `FonUserId` | Benutzer-ID (`benid`) |
| `FonUserPin` | PIN |

API: `POST` admin Fiskaly FON auth (see `AuthenticateFonRequest`). PIN is never logged.

SCU initialize **requires** FON authenticated (`FiskalySetupService`).

### B) FinanzOnline SOAP (outbox / RKSV submission)

Host config / `company_settings`: username + password + `TelematikId` + `HerstellerId`.  
`FinanzOnline:RksvSubmission:ClientKind=Real`, all `UseSimulation=false`. Details: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md).

---

## 3. Host configuration (do not paste secrets here)

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

Also set `Tse:Providers:fiskaly` to the same values. Env form: `Fiskaly__ApiKey`, `Fiskaly__ScuId`, …

After restart, Production fails closed if TSE is Soft/Demo/Fake or FON is still Simulation (`TseProductionOptionsValidator`, `ProductionRuntimeConfigurationGuard`).

---

## 4. Cash registers

1. Mandant cash registers exist in FA (not Decommissioned).
2. FA Fiskaly setup → initialize each register against the LIVE SCU.
3. Confirm FON registration of Kassen-ID + SCU (BMF), not only Fiskaly UI.

---

## 5. Verification (ComplianceOfficer must approve)

A LIVE “test payment” is a **real RKSV receipt**. Do **not** run POS `SMOKE_POS_PAYMENT=1` against Production.

Preferred order:

1. `GET /api/health/ready` and `GET /health/tse/mode` — fiscal posture Production / Device.
2. FA Fiskaly status: FON authenticated, SCU state healthy.
3. RKSV special receipt allowed by ComplianceOfficer (**Startbeleg** / **Nullbeleg**), not an arbitrary sale.
4. Confirm compact JWS + certificate thumbprint on the payment row; DEP export for that register.

Until that evidence exists, GO_LIVE C2 remains open.

---

## 6. Rollback

Keep TEST keys only on Development/Staging. Rolling Production back to TEST SCU or Soft TSE is a **No-Go** for paying mandants unless ComplianceOfficer documents an Ausfall path.
