# Fiscal smoke verification — 2026-10-28

Axis 3 smoke coverage only. No fiscal production code was changed.

**Branch:** `test/axis3-fiscal-smoke`  
**Verdict:** the five new smoke tests passed. The non-PostgreSQL suite passed. The PostgreSQL category did not execute its database tests on this machine. The BMF Prüftool fixture run returned PASS.

No fiscal code was modified in this change.

## Scope

Read-only review of the RKSV payment, signature, DEP export, and tenant-backup paths, then new tests only.

In scope:

1. `CreatePaymentAsync` persists the expected `payment_details` row.
2. The stored signature is a compact JWS (`header.payload.signature`, exactly three segments).
3. The receipt is written with the payment; the QR payload starts with `_R1-AT1_`; the previous-signature link is continuous across two sales.
4. DEP export places the signed receipts in `Belege-Gruppe`, reports pre-F5 versus F5 JWS, and groups by certificate thumbprint.
5. A tenant backup ZIP includes `payment_details` and `receipts`.

Out of scope: edits to `PaymentService`, `TseService`, `RksvDepExportService`, `SignaturePipeline`, migrations, and existing tests. No push. No pull request.

## What was verified (per step)

| Step | What the new test checked | Result |
|------|----------------------------|--------|
| 1 | One in-memory sale through `PaymentServiceCoverageHarness`. The saved `payment_details` row matches register, customer, amount, `AT-KASSE-01-` receipt number, TSE signature, previous signature, thumbprint, and tax number. | Pass |
| 2 | The harness TSE mock returns a real `SignaturePipeline.Sign` compact JWS. The saved `TseSignature` is that value, splits into exactly three non-empty segments, and the header is `eyJhbGciOiJFUzI1NiJ9`. | Pass |
| 3 | Two sequential sales. Each sale has one invoice and one receipt with the same `PaymentId`. Both receipt QR payloads and `GetQrPayloadForPaymentAsync` start with `_R1-AT1_`. The second payment and receipt store the first compact JWS as the previous signature. | Pass |
| 4 | Seeded in-memory register, same shape as `FiskalyDepExportPrueftoolTests`. Two F5 JWS plus one legacy JSON JWS on thumbprint A, one F5 JWS on thumbprint B. `GenerateDepExportWithValidationAsync` returns two groups, `F5CompliantJwsCount = 3`, `LegacyJwsCount = 1`, a legacy warning, and a non-empty `Signaturzertifikat` on each group. | Pass |
| 5 | `TenantScopedBackupExporter` ZIP for one tenant. Manifest counts are 1 for `payment_details.json` and `receipts.json`. The JSON entries contain the seeded receipt number, compact signature, and payment id. | Pass |

The BMF Prüftool was also run on the committed fixtures (`backend/Tests/fixtures/prueftool`). Machine-readable code checks 0–2 and `DEP-global` validation state were PASS. Exit code 0.

## What already existed

These tests were left unchanged. The new file does not repeat them.

| Area | Existing coverage |
|------|-------------------|
| Payment row and rollback | `PaymentServiceCreatePaymentCoreCoverageTests`, `PaymentServiceFiskalyCoverageTests` |
| Three-segment JWS without a payment row | `BelegdatenPayloadTests.SignaturePipeline_SignsRksvMachineCodePayload`, `RksvDepExportServiceTests.IsValidCompactJws_ValidatesThreePartJws` |
| Live `TseService` chain on PostgreSQL | `TseServiceSignatureChainPostgreSqlTests.CreateInvoiceSignature_OnPostgres_PersistsChain_AndValidates` (skipped here; Docker is down) |
| In-memory payment-to-receipt signature tests | `PaymentReceiptSignatureIntegrationTests` (three facts still `[Fact(Skip)]` because chain SQL is PostgreSQL-specific) |
| QR wire format | `RksvReceiptQrGenerationTests`, `BelegdatenPayloadTests` |
| Concurrent real TSE chain | `PostgreSqlOfflineReplayConcurrencyTests.ConcurrentPayments_SameRegister_RealTse_UniqueBelegNrAndMonotonicChain` (skipped here) |
| DEP structure, single thumbprint, F5 counter on a hand-built DTO | `RksvDepExportServiceTests`, `FiskalyDepExportPrueftoolTests` |
| Tenant ZIP product isolation | `TenantScopedBackupExporterTests` |
| Signature-chain snapshot restore | `TseBackupServiceTests.RestoreTseBackupAsync_RestoresChainAfterWipe` |
| Restore-drill HTTP | `AdminBackupContentValidationAndDrillEndpointTests.RunRestoreDrill_SettingsManage_Returns202` |
| Post-restore table list | `PostRestoreDrillSqlChecker` fiscal spine includes `payment_details`, `receipts`, and `signature_chain_state` |

## What was added

One new file: `backend/KasseAPI_Final.Tests/FiscalAxis3SmokeTests.cs`

1. `CreatePayment_PersistsExpectedPaymentDetailsRow`
2. `CreatePayment_StoresCompactJwsWithThreeSegments`
3. `CreatePayment_ReceiptQrAndPreviousSignatureAreContinuous`
4. `DepExport_GroupsByThumbprint_AndReportsLegacyJws`
5. `TenantBackup_IncludesPaymentDetailsAndReceiptRows`

No existing test file was modified.

## Pass/fail per step

| Step | Result | Evidence |
|------|--------|----------|
| 1. Payment row | Pass | `CreatePayment_PersistsExpectedPaymentDetailsRow` |
| 2. Compact JWS | Pass | `CreatePayment_StoresCompactJwsWithThreeSegments` |
| 3. Receipt, QR, previous signature | Pass | `CreatePayment_ReceiptQrAndPreviousSignatureAreContinuous` |
| 4. DEP groups and legacy JWS | Pass | `DepExport_GroupsByThumbprint_AndReportsLegacyJws` |
| 5. Tenant backup fiscal rows | Pass | `TenantBackup_IncludesPaymentDetailsAndReceiptRows` |
| Suite `Category!=PostgreSql` | Pass | Failed 0, Passed 5872, Skipped 5, Total 5877, Duration 46 s |
| Suite `Category=PostgreSql` | Pass with skips | Failed 0, Passed 1, Skipped 56, Total 57, Duration 144 ms |
| Prüftool fixtures | Pass | `DEP verification PASSED` (see command note below) |

New smoke class alone, before the full suite: Failed 0, Passed 5, Skipped 0, Total 5.

Exact summaries:

```text
Passed!  - Failed:     0, Passed:  5872, Skipped:     5, Total:  5877, Duration: 46 s - KasseAPI_Final.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:     1, Skipped:    56, Total:    57, Duration: 144 ms - KasseAPI_Final.Tests.dll (net10.0)
```

The single PostgreSQL-category pass is `DeReceiptSequenceTests.FormatDeBelegNr_IsSlugRegisterSequence`. It does not open a database. The other 56 tests in that filter called `Skip.IfNot` because the replay fixture had no database.

PostgreSQL integration tests re-run on 2026-10-28 and passed (Failed 0, Passed 57, Skipped 0, Total 57).

`pwsh` is not installed on this machine (`pwsh` is not recognized). The JAR `backend/Tests/regkassen-verification-depformat-1.1.1.jar` is present (29,716 bytes), so the Prüftool step was not skipped. The same script was run with Windows PowerShell:

```text
powershell -NoProfile -File ./scripts/rksv/verify-rksv-dep-export.ps1 -UseFixtures
```

Java was `C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot\bin\java.exe` (OpenJDK 17.0.19). Result: `DEP verification PASSED`, exit code 0. Output directory `verification_output/` is gitignored.

The five skips in `Category!=PostgreSql` are the three `PaymentReceiptSignatureIntegrationTests` facts, `DemoProductImportServiceTests.ImportDemoProductsAsync_PostgreSql_DoubleImport_NoDuplicateCategoryErrors`, and `RksvDepPrueftoolFixtureTests.DumpFixturePkcs8`.

## Defects found

None. The five new tests passed. No fiscal defect was opened by this run.

## Deliberately not fixed

- `signature_chain_state` is not an entry in the tenant ZIP. `TenantScopedBackupExporter` writes `payment_details.json`, `receipts.json`, and `tse_signatures.json`. Chain state stays on `TseBackupService` and on a system `pg_dump` restore. That was not changed.
- The three skipped `PaymentReceiptSignatureIntegrationTests` facts were not un-skipped and were not rewritten.
- `PostRestoreDrillSqlChecker` was not given a new drill that re-reads fiscal rows from a restored PostgreSQL database.

## Operator decisions recorded

- Do not assert `signature_chain_state` inside the tenant ZIP. Policy text says "signature metadata", and `tse_signatures.json` satisfies that. Requiring `signature_chain_state` in the tenant ZIP would be a separate defect report, not this branch.
- Do not duplicate the PostgreSQL-locked chain tests (`TseServiceSignatureChainPostgreSqlTests`, `ConcurrentPayments_SameRegister_RealTse_UniqueBelegNrAndMonotonicChain`).
- Do not duplicate `AdminBackupContentValidationAndDrillEndpointTests.RunRestoreDrill_SettingsManage_Returns202`.

## Open risks

- On the first run, Docker Desktop was not running (`npipe:////./pipe/dockerDesktopLinuxEngine` was missing), so `Category=PostgreSql` skipped the live signature-chain, payment-lifecycle, and backup-concurrency tests. PostgreSQL integration tests re-run on 2026-10-28 and passed (Failed 0, Passed 57, Skipped 0, Total 57).
- Steps 2 and 3 store a compact JWS returned by a mock `ITseService`. `SignaturePipeline.Sign` builds that JWS in the test. `TseService.CreateInvoiceSignatureAsync` and its `signature_chain_state` SQL lock are not on this in-memory path.
- DEP grouping was checked with one mocked leaf certificate returned for both thumbprints. The export still emitted two `Belege-Gruppe` entries, keyed by the stored thumbprint strings. This does not prove two different certificate binaries.
- `pwsh` (PowerShell 7) is not on PATH. The documented `pwsh ./scripts/rksv/verify-rksv-dep-export.ps1 -UseFixtures` command cannot be run until PowerShell 7 is installed. Windows PowerShell 5.1 ran the same script successfully today.

## Next steps

- Operator review of `test/axis3-fiscal-smoke` before any pull request.
- Do not push to `main`. Do not open a pull request until that review says so.
- Re-run `dotnet test backend/KasseAPI_Final.sln --filter "Category=PostgreSql"` with Docker running, or with `REGKASSE_TEST_POSTGRES` set, when a live chain check is required.
- Install PowerShell 7 if the documented `pwsh` Prüftool command must succeed unchanged.

No fiscal code was modified in this change.
