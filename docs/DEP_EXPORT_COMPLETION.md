# RKSV DEP §7 Export - Completion Report

**Date:** 2026-06-11  
**Status:** ✅ All phases complete (F1–F5)

## Implementation Summary

All 5 phases completed:

| Phase | Description | Status |
|-------|-------------|--------|
| F1 | Controller + Service + DTO | ✅ |
| F2 | Certificate grouping + CA chain | ✅ |
| F3 | Special receipts + DailyClosing | ✅ |
| F4 | Prüftool test script | ✅ |
| F5 | Full RKSV §9 compliance | ✅ |

### Deliverables

| Area | Key files |
|------|-----------|
| API | `backend/Controllers/AdminRksvDepExportController.cs` |
| Export service | `backend/Services/RksvDepExportService.cs` |
| DTOs | `backend/Models/Export/RksvDepExportDtos.cs` |
| RKSV §9 signing | `backend/Tse/BelegdatenPayload.cs`, `BelegdatenPayloadBuilder.cs`, `RksvMachineCodeBuilder.cs`, `SignaturePipeline.cs` |
| TSE integration | `backend/Services/TseService.cs`, `backend/Tse/ITseKeyProvider.cs` |
| Prüftool script | `scripts/verify-rksv-dep-export.ps1` |
| Unit tests | `backend/KasseAPI_Final.Tests/RksvDepExportServiceTests.cs`, `BelegdatenPayloadTests.cs` |

### F5 — RKSV §9 fields (signing + Prüftool)

JWS payload is BMF machine code (`_R1-AT1_{Kassen-ID}_{Belegnummer}_…`), not QR payload or simplified JSON.

| BMF field | Implementation |
|-----------|----------------|
| `Kassen-ID` | `BelegdatenPayload.KassenId` |
| `Belegnummer` | Receipt number |
| `Beleg-Datum-Uhrzeit` | Vienna local ISO 8601 |
| `Betrag-Satz-Normal` … `Betrag-Satz-Besonders` | Five gross tax buckets (`RksvTaxSetMapper`) |
| `Stand-Umsatz-Zaehler-AES256-ICM` | AES-256-CTR (`RksvTurnoverCounterCrypto`) |
| `Zertifikat-Seriennummer` | TSE cert serial |
| `Sig-Voriger-Beleg` | SHA-256 chain hash (`RksvChainingValue`) |

**Note:** Receipts signed before F5 use legacy JSON JWS payloads and will not pass BMF receipt verification in Prüftool runs.

## API Endpoint

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

**Permissions:** `ReportExport` + `AuditView`  
**Audit:** `RksvDepExportJson` on every successful export

## Testing

### Unit tests

```bash
cd backend
dotnet test --filter "RksvDepExportServiceTests"
dotnet test --filter "BelegdatenPayloadTests"
```

### BMF Prüftool (DEP format)

Requires JDK 17+ on PATH and `backend/Tests/regkassen-verification-depformat-1.1.1.jar`.

```powershell
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures
```

Committed fixtures: `backend/Tests/fixtures/prueftool/` (`dep-export.json`, `crypto-material.json`). Regenerate: `.\scripts\generate-dep-export-fixtures.ps1`.

Expected: exit code **0** = PASS.

## Migration Required

Apply pending EF migrations before using thumbprint grouping and F5 turnover counter:

```bash
dotnet ef database update --project backend/KasseAPI_Final.csproj
```

| Migration | Purpose |
|-----------|---------|
| `20260611023458_DepExportCertificateAndTurnoverColumns` | `payment_details.certificate_thumbprint`, `DailyClosings.certificate_thumbprint`, `signature_chain_state.last_turnover_counter_cents` |

## Related documentation

- `docs/DEP_EXPORT_DEVELOPMENT.md` — developer guide (architecture, troubleshooting)
- `AGENTS.md` — Fiscal Rules → DEP §7 Export
- `.cursor/rules` — RKSV DEP §7 Export Rules (Updated)
