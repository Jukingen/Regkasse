# RKSV DEP §7 Export Development Guide

**Status:** ✅ Implemented (F1–F5 complete, 2026-06-11)

## Overview

DEP (Datenerfassungsprotokoll) export is a BMF-required feature for tax audits (Signaturjournal). It exports all fiscal receipts and TSE signatures for one cash register and UTC period in the official BMF JSON format (`Belege-Gruppe`).

**Not the same as:** operational fiscal CSV/JSON exports under `FiscalExportController` — DEP is audit-specific and uses compact JWS only.

**Features:** BMF `Belege-Gruppe` JSON, certificate grouping by thumbprint, normal + Sonderbelege + Tagesabschluss, compact JWS (not QR), RKSV §9 machine-code JWS payload, chronological ordering within each certificate group.

## Architecture

```
AdminRksvDepExportController
        │
        ▼
RksvDepExportService
        │
        ├── PaymentDetails (normal + special receipts)
        ├── DailyClosings (Tagesabschluss signatures)
        └── ITseKeyProvider (leaf cert + CA chain per thumbprint)
        │
        ▼
RksvDepExportRootDto  →  BMF JSON response
```

| Layer | Path |
|-------|------|
| Controller | `backend/Controllers/AdminRksvDepExportController.cs` |
| Service | `backend/Services/RksvDepExportService.cs` |
| DTOs | `backend/Models/Export/RksvDepExportDtos.cs` |
| Receipt row model | `backend/Models/Export/RksvDepReceiptSignatureInfo.cs` |
| TSE certs / chain | `backend/Tse/ITseKeyProvider.cs`, `TseCertificateChainBuilder.cs` |
| RKSV §9 signing | `backend/Tse/BelegdatenPayload.cs`, `BelegdatenPayloadBuilder.cs`, `RksvMachineCodeBuilder.cs`, `SignaturePipeline.cs`, `Services/TseService.cs` |
| Prüftool script | `scripts/verify-rksv-dep-export.ps1` |
| Unit tests | `backend/KasseAPI_Final.Tests/RksvDepExportServiceTests.cs`, `BelegdatenPayloadTests.cs` |

## API

```
GET /api/admin/rksv/dep-export
```

| Parameter | Required | Default |
|-----------|----------|---------|
| `cashRegisterId` | Yes | — |
| `fromUtc` | Yes | — |
| `toUtc` | Yes | — (max 366 days) |
| `includeSpecialReceipts` | No | `true` |
| `includeDailyClosings` | No | `true` |

**Permissions:** `ReportExport` + `AuditView` (`report.export`, `audit.view`).

**Audit:** Every successful export logs `RksvDepExportJson`.

**Example (development):**

```bash
curl -H "Authorization: Bearer {token}" \
     -H "X-Tenant-Id: {tenant}" \
     "http://localhost:5184/api/admin/rksv/dep-export?cashRegisterId={guid}&fromUtc=2026-01-01T00:00:00Z&toUtc=2026-01-31T23:59:59Z"
```

## Download (stored history)

Completed exports are written under `DepExportStorage` (default `App_Data/dep-exports/{tenantId}/`) and listed with `hasStoredFile` / `downloadUrl`.

| Method | Path | Notes |
|--------|------|-------|
| `GET` | `/api/admin/rksv/dep-export/download/{id}` | Auth download by history id (canonical) |
| `GET` | `/api/admin/rksv/dep-export/history/{id}/download` | Alias |
| `POST` | `/api/admin/rksv/dep-export/download/{id}/token` | Issue/rotate opaque token (default TTL **24h**) |
| `GET` | `/api/admin/rksv/dep-export/download/token/{token}` | Token download (JWT + permissions still required) |

Every attempt is audited (`DepExportAuditActions.Downloaded` → `AuditEventType.RksvDepExportDownloaded`). Cross-tenant → HTTP **404**.

Migration: `20260807120000_AddDepExportHistoryDownloadToken` (`download_token`, `download_token_expires_at_utc`, `expires_at`, `downloaded_at`).

---

## Data sources

| Source | Receipt type | Date filter | Thumbprint column |
|--------|--------------|-------------|-------------------|
| `payment_details` | `Normal` (kind null) | `CreatedAt` | `certificate_thumbprint` |
| `payment_details` | Sonderbeleg kinds | `CreatedAt` | `certificate_thumbprint` |
| `DailyClosings` | `DailyClosing` | `ClosingDate` | `certificate_thumbprint` |

Legacy rows without thumbprint fall back to the active TSE signing certificate.

## Phase status

| Phase | Description | Status |
|-------|-------------|--------|
| F1 | Controller + service + DTO | Complete |
| F2 | Certificate grouping + CA chain | Complete |
| F3 | Special receipts + daily closings | Complete |
| F4 | Prüftool test script | Complete |
| F5 | Full RKSV §9 payload Prüftool compliance | Complete |

### F5 — BelegdatenPayload (RKSV §9)

JWS payload is the BMF **machine code** string (`_R1-AT1_{Kassen-ID}_{Belegnummer}_…`), not a simplified JSON blob.

| BMF field | Implementation |
|-----------|----------------|
| `Kassen-ID` | `BelegdatenPayload.KassenId` |
| `Belegnummer` | Receipt number |
| `Beleg-Datum-Uhrzeit` | ISO 8601 Vienna local (`yyyy-MM-ddTHH:mm:ss`) |
| `Betrag-Satz-Normal` … `Betrag-Satz-Besonders` | Five gross buckets via `RksvTaxSetMapper` |
| `Stand-Umsatz-Zaehler-AES256-ICM` | AES-256-CTR via `RksvTurnoverCounterCrypto` |
| `Zertifikat-Seriennummer` | TSE cert serial |
| `Sig-Voriger-Beleg` | SHA-256 chain (`RksvChainingValue`) |

Key files: `backend/Tse/BelegdatenPayload.cs`, `BelegdatenPayloadBuilder.cs`, `RksvMachineCodeBuilder.cs`, `SignaturePipeline.cs`, `TseService.cs`.

Migration: `20260611023458_DepExportCertificateAndTurnoverColumns` adds thumbprint columns and `signature_chain_state.last_turnover_counter_cents`.

**Note:** Receipts signed before F5 use the legacy JSON JWS payload and will not pass BMF receipt verification.

### Legacy (pre-F5) JWS detection (P2-2)

| Format | JWS payload (middle segment) | Prüftool beleg verify |
|--------|------------------------------|------------------------|
| **F5** | RKSV §9 machine code starting with `_R1-…` (`SignaturePipeline.Sign`) | Expected to pass |
| **Pre-F5 / legacy** | JSON `BelegdatenPayload` (e.g. Soft/Fake TSE historical rows) | Likely fail |

Detection: `SignaturePipeline.IsF5CompliantJws` / `RksvDepExportService.IsF5CompliantJws`.

Export envelope (`includeEnvelope=true`) and history metadata expose:

- `legacyJwsCount`, `f5CompliantJwsCount`, `legacyJwsWarning`, `prueftoolCompatible`
- History column `legacy_jws_count` (migration `20260729230000_AddDepExportHistoryLegacyJwsCount`)
- FA: warning alert on DEP export page + “Prüftool-kompatibel” history column

**Compliance decision — no automatic re-sign:** Rewriting stored TSE signatures would break the fiscal signature chain. Operators should treat legacy rows as inventory for Prüftool awareness only; new receipts must be signed with F5. A future re-sign tool would require an explicit Compliance/Ops change request (out of scope for P2-2).

## Testing

### Unit tests

```bash
cd backend
dotnet test --filter "RksvDepExportServiceTests"
dotnet test --filter "BelegdatenPayloadTests"
```

### Prerequisites (BMF Prüftool)

1. JDK 17+ installed (`java` on PATH, or `JAVA_HOME` / `PRUEFTOOL_JAVA`)
2. BMF DEP JAR + dependencies under `backend/Tests/` (gitignored — see below)
3. Generated `dep-export.json` from the API, or committed fixtures under `backend/Tests/fixtures/prueftool/dep-export.json`
4. `crypto-material.json` — BMF cryptographic material container (`backend/Tests/fixtures/prueftool/crypto-material.json`; dev-only, not production secrets)

#### Install JARs (local or CI)

Official release: [BMF Prüftool V1.1.1](https://github.com/BMF-RKSV-Technik/at-registrierkassen-mustercode/releases/tag/V1.1.1) (`regkassen-verification-1.1.1.zip`).

```powershell
pwsh ./scripts/ensure-bmf-prueftool.ps1
```

This downloads the ZIP (SHA256-pinned), then copies:

- `backend/Tests/regkassen-verification-depformat-1.1.1.jar`
- `backend/Tests/regkassen-verification-receipts-1.1.1.jar`
- `backend/Tests/lib/*.jar`

JARs remain gitignored (~19 MB). Re-run with `-Force` to refresh.

### Run verification

```powershell
# Committed fixtures (recommended for local/CI smoke)
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures

# Explicit fixture paths (same as -UseFixtures)
.\scripts\verify-rksv-dep-export.ps1 `
  -DepExportPath "./backend/Tests/fixtures/prueftool/dep-export.json" `
  -CryptoMaterialPath "./backend/Tests/fixtures/prueftool/crypto-material.json"

# Custom export from API
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"
```

### CI (GitHub Actions)

Workflow: [`.github/workflows/dep-prueftool.yml`](../.github/workflows/dep-prueftool.yml)

| Step | What |
|------|------|
| `actions/setup-java@v4` | Temurin JDK **17** |
| `ensure-bmf-prueftool.ps1` | Official BMF ZIP → `backend/Tests/` (cached) |
| `verify-rksv-dep-export.ps1 -UseFixtures` | Hard-fail smoke on committed fixtures |
| `dotnet test --filter Category=DepPrueftool` | (1) committed fixtures via runner helper; (2) **seeded in-memory DB** → `RksvDepExportService.GenerateDepExportAsync` → live Prüftool PASS (`FiskalyDepExportPrueftoolTests`) |

Triggers on `backend/**` and related script/workflow path changes (`pull_request` / `push` to `main`/`master`).

Regenerate fixtures:

```powershell
.\scripts\generate-dep-export-fixtures.ps1
```

Verbose / detailed Java output:

```powershell
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json" -DetailedOutput
```

(`-DetailedOutput` passes `-d` to the BMF checker.)

### Expected output

- **Exit code 0** = PASS
- `verification_output/DEP-global.json` contains the verification summary
- On failure, the script prints `DEP-global.json` to the console

## Common issues

### Empty `Signaturzertifikat` (P2-3 hard-fail)

**Problem:** Leaf signing certificate cannot be resolved for a thumbprint group that has compact JWS receipts.

**Behavior:** `GenerateDepExportAsync` throws `RksvDepExportCertificateMissingException`. Admin API returns **HTTP 500** with `code: RKSV_DEP_EXPORT_MISSING_CERTIFICATE` and `thumbprint`. Empty `Signaturzertifikat` is never emitted in BMF JSON.

**Solution:** Ensure payments/closings store a valid `certificate_thumbprint` and `ITseKeyProvider.GetCertificateByThumbprintAsync` can load the leaf DER for that thumbprint.

### Certificate chain missing

**Problem:** `Zertifizierungsstellen` empty when a production PKI chain is expected.

**Solution:** Ensure `ITseKeyProvider.GetCertificateChainAsync(thumbprint)` returns issuer CAs. Software dev TSE uses self-signed certs (chain often empty — expected in dev).

### Wrong signature format

**Problem:** QR payload used instead of compact JWS.

**Solution:** Export `TseSignature` / `DailyClosings.TseSignature` columns only. Valid compact JWS has exactly three Base64URL segments separated by `.` (see `RksvDepExportService.IsValidCompactJws`).

### Chronological order broken

**Problem:** `Belege-kompakt` not in audit order within a certificate group.

**Solution:** Rows are ordered by `IssuedAt`, then `SequenceNumber` (BelegNr seq from `AT-{TSE}-{YYYYMMDD}-{SEQ}` or closing date `YYYYMMDD`). See `OrderReceiptsForDepExport`.

## Troubleshooting

| Error | Solution |
|-------|----------|
| Java not found | Install JDK 17+, add `java` to PATH |
| JAR not found | Run `pwsh ./scripts/ensure-bmf-prueftool.ps1` |
| DEP format invalid | Re-run with `-DetailedOutput` for detailed checker output |
| Empty `Belege-Gruppe` | No signed rows in period, or all JWS failed validation |
| `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` (500) | Leaf cert missing for thumbprint group — fix TSE key material / thumbprint stamp |
| Register not found (404) | Wrong `cashRegisterId`, missing tenant context, or cross-tenant access |

## Related documentation

- `docs/DEP_EXPORT_COMPLETION.md` — completion report (F1–F5 summary, migrations)
- `AGENTS.md` — Fiscal Rules → DEP §7 Export
- `backend/README.md` — RKSV / TSE → DEP §7 Export
- `.cursor/rules` — RKSV DEP §7 Export Rules (Updated)
- `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md` — broader BMF receipt check workflow
