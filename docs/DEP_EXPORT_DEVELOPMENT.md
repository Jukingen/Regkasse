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

**Note:** Receipts signed before F5 use the legacy JSON JWS payload and will not pass BMF receipt verification; re-sign or accept only new receipts for Prüftool runs.

## Testing

### Unit tests

```bash
cd backend
dotnet test --filter "RksvDepExportServiceTests"
dotnet test --filter "BelegdatenPayloadTests"
```

### Prerequisites (BMF Prüftool)

1. JDK 17+ installed (`java` on PATH)
2. BMF DEP JAR: `backend/Tests/regkassen-verification-depformat-1.1.1.jar`
3. Dependency JARs: `backend/Tests/lib/*.jar`
4. Generated `dep-export.json` from the API, or committed fixtures under `backend/Tests/fixtures/prueftool/dep-export.json`
5. `crypto-material.json` — BMF cryptographic material container (`backend/Tests/fixtures/prueftool/crypto-material.json`; dev-only, not production secrets)

### Run verification

```powershell
# Committed fixtures (recommended for local/CI smoke)
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures

# Custom export from API
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"
```

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
| JAR not found | Ensure `backend/Tests/regkassen-verification-depformat-1.1.1.jar` and `backend/Tests/lib/` exist |
| DEP format invalid | Re-run with `-DetailedOutput` for detailed checker output |
| Empty `Belege-Gruppe` | No signed rows in period, or all JWS failed validation |
| Register not found (404) | Wrong `cashRegisterId`, missing tenant context, or cross-tenant access |

## Related documentation

- `docs/DEP_EXPORT_COMPLETION.md` — completion report (F1–F5 summary, migrations)
- `AGENTS.md` — Fiscal Rules → DEP §7 Export
- `backend/README.md` — RKSV / TSE → DEP §7 Export
- `.cursor/rules` — RKSV DEP §7 Export Rules (Updated)
- `docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md` — broader BMF receipt check workflow
