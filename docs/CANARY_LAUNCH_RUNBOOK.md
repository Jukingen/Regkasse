# Canary launch runbook (CH, DE, Peppol)

**Last updated:** 2026-09-30  
**Host:** Staging only (`ASPNETCORE_ENVIRONMENT=Staging`).  
**Hubs:** [`COUNTRIES.md`](COUNTRIES.md) §16 · [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) · [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) · [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md)

Order: Staging smoke, then one canary tenant, then Production. This file is the Staging canary sequence. It does not promote a flag and it does not open LIVE HTTP.

Restart the Staging API after a config or secret change. `Peppol:ReservedExit` and the host fiscal locks are read at startup. The process does not write those keys.

Do not print API keys, IBANs, UBL, or signature bytes into the ticket. Record match / no-match and ids only.

---

## 1. Preconditions (common)

| Check | Pass |
|-------|------|
| Operator | Super Admin. Permission `system.critical`. |
| API host | `ASPNETCORE_ENVIRONMENT=Staging`. |
| Secret store | The Staging deployment secret store is reachable. Keys stay there. They are not committed. |
| FA | `https://admin.staging.regkasse.at` |

One tenant per country. Do not reuse an Austrian mandant that already has RKSV-signed receipts for a DE or CH country change. See §6.

`QrRechnung:BankSubmit:Enabled` stays **false**. Production and Staging refuse `true` at startup. There is no bank HTTP client.

---

## 2. CH canary (lowest risk)

QR-Rechnung Option A is an operator download plus a bank-portal note. Regkasse does not call a bank. There is **no** `QrRechnung:CanaryTenantId` key. A CH sale that builds a QR is locked by `Mwst:CanaryTenantId` ([`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md)). PDF download is a separate gate: every open gap accepted, and bank submit left off.

### Config

| Key | Value |
|-----|--------|
| `Mwst:CanaryTenantId` / env `Mwst__CanaryTenantId` | One CH tenant guid (format `D`). Empty denies every tenant. |
| `KassenSicherheit:Provider` | `not-configured` (unused for CH). |
| `Mwst:UseTestEndpoint` | `false`. `true` fails Staging startup. |
| `QrRechnung:BankSubmit:Enabled` | `false`. |
| `QrRechnung:BuilderMode` | `not-configured`. `dryRun` fails Staging startup. |

### FA — accept the five known gaps

Open `/admin/tenants/{tenantId}` → **ChQrGapAcceptancePanel**. Accept all five ids that are `present: false` in `ChQrKnownGaps.json`:

- `official-swiss-cross`
- `font-embedding-liberation-arial`
- `perforation-line`
- `pain001`
- `bank-scan`

Acceptance does not change the PDF and does not enable bank submission. Audit `ChQrKnownGapsAccepted` is **114**. Activity `ChQrKnownGapsAccepted` is **260**. An open gap that is not accepted can raise activity `ChQrKnownGapsOutstanding` (**261**). The invoice is not blocked.

### Smoke

1. Create one CH invoice for that tenant (POS sale on a CH register, or the invoice flow that builds the QR payload).
2. On the invoice detail, download the QR-Rechnung PDF. `GET /api/admin/tenants/{tenantId}/ch-qr-invoices/{invoiceId}/pdf` returns the PDF only when every open gap is accepted and `QrRechnung:BankSubmit:Enabled` is false. Otherwise HTTP **409** `CH_QR_GAPS_NOT_ACCEPTED`.
3. Confirm the payment-part payload text starts with `SPC` (version `0200`) and the IBAN matches the tenant bank account. Record yes/no. Do not paste the IBAN into the ticket.
4. After the file is in the bank portal, mark **uploaded to bank** (Option A). `POST /api/admin/tenants/{tenantId}/ch-qr-invoices/{invoiceId}/upload-confirmation` with `uploadedBy`, `uploadedAtUtc`, and `bankReference`. This requires a `QrRechnungPdfGenerated` audit row first.

### Verify

| Check | Pass |
|-------|------|
| Audit **116** | `QrRechnungPdfDownloaded` for that invoice. |
| Audit **117** | `QrRechnungBankUploadConfirmed` for that invoice. |
| Activity | No error. `ChQrKnownGapsOutstanding` (261) is absent after the five gaps are accepted. |

Neither call stores a bank key or opens bank HTTP.

### Rollback

Clear `Mwst:CanaryTenantId` (set `Mwst__CanaryTenantId` empty) and restart. New CH QR sales are denied. Optional: FA `/admin/mwst` **Canary zurücknehmen** sets `Fiscal.MwstCh=false` for the configured canary tenant (`AuditEventType.ChMwstCanaryRolledBack`). That POST does not take a tenant id in the body; it uses `Mwst:CanaryTenantId`.

Gap-acceptance rows and audit 116 / 117 stay. A file already uploaded at the bank cannot be unsent from Regkasse. Do not set `QrRechnung:BankSubmit:Enabled=true`.

---

## 3. DE canary (medium risk)

One DE tenant. Staging SIGN DE **TEST** only. This is not a live TSE.

### Config

Set secrets on the Staging host. Do not commit them.

```text
KassenSicherheit__ApiKey=<test-key>
KassenSicherheit__ApiSecret=<test-secret>
KassenSicherheit__AdminPin=<test-pin>
```

| Key | Value |
|-----|--------|
| `KassenSicherheit:PilotMode` | `true` |
| `KassenSicherheit:Provider` | `fiskaly-de` |
| `KassenSicherheit:Environment` | `TEST` |
| `KassenSicherheit:ApiBaseUrl` | `https://kassensichv-middleware.fiskaly.com/api/v2` |

`PilotMode=true` with `Environment=LIVE`, or the SIGN DE LIVE host `kassensichv.fiskaly.com`, fails startup. `PilotMode=false` does not turn DE signing on.

`Fiscal.KassenSicherheitDe` stays off from the country profile until you set a **tenant override** `true` for exactly one DE tenant (`tenant_settings`). `PilotMode` does not set that flag. Do not omit `tenantId` on the flag write.

### FA

Open `/admin/kassensicherheit`, select that tenant, Status tab.

Pass: provider `fiskaly-de`, environment `TEST`, flag on, TSS id and client id present. The page is read-only. It does not show a Ready label and it does not edit TSS or client ids.

`GetStatusAsync` reports Ready only when the SIGN DE TSS state is `INITIALIZED`. Confirm that state from the fiskaly TEST organization (TSS `POST /tss`, then `PATCH` to `INITIALIZED`, then a client). Do not treat a missing "Ready" string on the FA card as a failure.

### Smoke

One POS sale on that tenant's register.

`npm run smoke:kassensicherheit-test` is a dry-run and does not call fiskaly. Real HTTP is separate and still not LIVE:

```bash
set KASSENSICHERHEIT_SMOKE_ALLOW=1
node scripts/smoke/kassensicherheit-test-smoke.mjs --confirm
```

`--confirm` without the three credentials, or without `KASSENSICHERHEIT_SMOKE_ALLOW=1`, exits 1 and does not send HTTP.

### Verify

`de_tse_signatures` has **no** `provider_message_id` and **no** `provider_status`. Those names are on `einvoice_submissions` (Peppol), not on this table.

```sql
SELECT id, tenant_id, payment_details_id, tss_id, transaction_id,
       signature_algorithm, signed_at_utc, certificate_serial
FROM de_tse_signatures
WHERE tenant_id = '<de-tenant-guid>'
ORDER BY signed_at_utc DESC
LIMIT 5;
```

Pass: one row for the sale with non-empty `tss_id`, `transaction_id`, and `signature` (do not copy `signature` into the ticket).

```sql
SELECT tse_signature, prev_signature_value_used, certificate_thumbprint
FROM payment_details
WHERE id = '<payment_details_id>';
```

Pass: all three are null. DE does not write the Austrian RKSV chain columns.

Audit **101** (`KsDeTxStarted`) and **102** (`KsDeTxFinished`) exist as event numbers. The payment router does not write them on `SignDeAsync` today. A missing 101/102 row is not, by itself, a failed signature when `de_tse_signatures` has the payment. First-time TSS or client id storage writes audit **100** (`KsDeTssCreated`). A DSFinV-K export request writes audit **103** (`KsDeExportCreated`) and returns status `PENDING` only (no archive download).

### Rollback

1. Tenant override `Fiscal.KassenSicherheitDe=false` for that tenant only.
2. `KassenSicherheit:PilotMode=false`, then restart.

`de_tse_signatures` rows stay. Do not delete them. Do not copy them onto `payment_details`.

---

## 4. Peppol canary (medium risk)

TEST Storecove, one tenant. `EInvoicing.Peppol` stays in `FeatureFlagNames.Reserved`. HTTP 2xx is `Sent`, not `Ack`. This section does not promote the flag. Details: [Peppol canary operations](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-canary-operations).

### Config

Production `ReservedExit:Enabled` stays false. `Peppol:Storecove:Environment=LIVE` together with `ReservedExit:Enabled=true` fails startup.

| Key | Value |
|-----|--------|
| `Peppol:ReservedExit:Enabled` | `true` (Staging / TEST deployment only) |
| `Peppol:ReservedExit:CanaryTenantId` | One real TEST tenant guid, format `D` |
| `Peppol:ReservedExit:ApprovedBy` | `ops/<github-login>` of the person who sets `Enabled` |
| `Peppol:ReservedExit:ApprovedAtUtc` | UTC time of that change. The process does not fill it. |
| `Peppol:Provider` | `storecove` (default `not-configured` does not send) |
| `Peppol:Storecove:Environment` | exactly `TEST` |
| `Peppol:AckPollInterval` | Minutes. **Default 0 opens no poll HTTP**, so a `Sent` row will not move to `Ack`. Set a value greater than 0 only when this canary should poll. |

`SubmitAsync` opens HTTP only when `Enabled=true`, the tenant id equals `CanaryTenantId`, `Provider=storecove`, and `Environment` is exactly `TEST`. Any other tenant stays `Queued` / `peppol-reserved` and does not open HTTP.

### Secret

`Peppol__Storecove__ApiKey` in the deployment secret store. Do not put the value in appsettings or in git. Do not print it.

### FA — participant

`/admin/peppol/participants` (`system.critical`). Register one row for the canary tenant:

| Field | Rule |
|-------|------|
| `ap_environment` | `TEST` |
| `participant_id` | Required |
| `legal_entity_id`, `eidentifier_scheme`, `eidentifier_value` | All set by the operator. Not derived from a VAT id. |

There is no credential column and no update API. `POST /api/admin/peppol/participants` inserts a row. `GET /api/admin/peppol/participants/{tenantId}` lists that tenant. The list DTO does not return `updated_at_utc`.

The backend lead confirms the tenant row and `ap_environment=TEST` before `Enabled` is turned on.

### Smoke

`npm run smoke:peppol-test` prints the plan and does **not** call Storecove.

Real TEST HTTP needs every gate:

```bash
set PEPPOL_SMOKE_ALLOW=1
set Peppol__Storecove__ApiKey=<secret-store value>
set PEPPOL_SMOKE_LEGAL_ENTITY_ID=<operator legal entity id>
set PEPPOL_SMOKE_SCHEME=<operator scheme>
set PEPPOL_SMOKE_VALUE=<operator identifier>
npm run smoke:peppol-test -- --confirm
```

`--confirm` without `PEPPOL_SMOKE_ALLOW=1` exits 1 and does not send HTTP. Success prints `provider_message_id` and `einvoice_submissions_status`. Failure prints `http_status` and `provider_code` only. The script is not a CI job. `LIVE` stays unsent.

### Verify

FA `/admin/peppol/submissions`, filter to the canary tenant.

| Observation | Meaning |
|-------------|---------|
| `Sent`, `provider_message_id` set, `failure_reason` null | POST 2xx stored. Not an ACK. |
| `Ack`, `acked_at_utc` set, `failure_reason` null, `provider_message_id` set | ACK. Poll saw `state=DELIVERED` and a matching `guid`. Audit `EinvoiceAckReceived` is **115**. Activity `EinvoiceAckReceived` is **262**. |
| Still `Sent` after the smoke | Expected while `Peppol:AckPollInterval` is 0. |
| `Failed` / `peppol-ack-timeout` | The `Sent` row is older than 24 hours without a delivered poll. |

Do not hand-edit a row to `Ack`. If the ACK is ambiguous, set `ReservedExit:Enabled=false` and leave `TestAckReceivedAtUtc` null.

### Rollback

Set `Peppol:ReservedExit:Enabled=false` and restart. Later `SubmitAsync` calls do not open HTTP. `einvoice_submissions` rows stay (`Queued`, `Sent`, `Ack`, or `Failed`). Do not delete them. A document Storecove already accepted cannot be unsent from Regkasse. Keep `participant_id`. Unregister only in the provider portal.

---

## 5. Post-canary: promotion

Do not promote from this document.

Peppol promotion is [Peppol promotion runbook (22-b-5)](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-promotion-runbook-22-b-5). It starts only after a verified ACK (`status=Ack`, `acked_at_utc` set, `failure_reason` null, `provider_message_id` set). The backend lead opens the PR that moves `EInvoicing.Peppol` from `Reserved` to `All`. This file does not open that PR, does not set `ReservedExit:Enabled` for promotion, and does not set `Environment=LIVE`.

CH bank submit and DE Production SIGN DE are not part of that promotion.

---

## 6. Super Admin do-not list

- Do not set `Fiscal.KassenSicherheitDe=true` for every tenant, as a country-profile default, or as a Production default. One Staging DE tenant override only.
- Do not set `QrRechnung:BankSubmit:Enabled=true` in Production or Staging. Startup rejects it (`CountryFiscalLockEvaluator`, `ReasonQrBankSubmit`). There is no bank client. Option B (EBICS / pain.001) is out of scope.
- Do not move `EInvoicing.Peppol` out of `FeatureFlagNames.Reserved` without the promotion PR in §5. Do not set `Peppol:Storecove:Environment=LIVE` with `ReservedExit:Enabled=true`.
- Do not change a tenant's country when signed fiscal documents were issued under another fiscal system. `AdminTenantService.UpdateCountryAsync` returns HTTP **409** `FISCAL_COUNTRY_CHANGE_INVALID`.

Also leave these Staging locks alone: `KassenSicherheit:Provider=fake` and `AllowSimulatedTse=true` fail startup; `Mwst:UseTestEndpoint=true` fails startup; `QrRechnung:BuilderMode=dryRun` fails startup.

---

## 7. Disclaimer

This is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance.
