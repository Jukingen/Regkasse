# Fiscal export: diagnostic vs audit handoff vs compliance

## Purpose

The fiscal export HTTP API (`GET /api/admin/fiscal-export`) builds a **JSON package** of receipts, TSE/RKSV signature material, chain hints, and closings for a **single cash register** and **UTC time window**. The pipeline is shared; **intent** is selected with the `exportProfile` query parameter.

This document separates **operator-facing diagnostic use** from **controlled handoff** to auditors or compliance counsel. It does **not** replace FinanzOnline processes, DEP/A-SIT procedures, or statutory cash-register obligations.

## Profiles

| Profile | Query value | Permissions | Intended use |
|--------|-------------|-------------|--------------|
| **Diagnostic** | `diagnostic` (default) | `report.export` | Support, engineering, incident triage. Integrity booleans are **slice-scoped** and **best-effort**. |
| **Audit handoff** | `audit_handoff` | `report.export` + `audit.view` | Structured bundle for **third-party or internal audit** review, **alongside** the audit trail — not sole legal proof. |
| **Compliance / legal review** | `compliance` | `report.export` + `audit.view` + `fiscal.export.compliance` | Evidence pack for **external compliance or legal review** workflows; assigned only to roles that carry `fiscal.export.compliance` in the role matrix. |

Every payload includes:

- `notLegalProofNotice` — **mandatory**: this file is **not** a statutory RKSV attestation.
- `exportProfile` / `exportProfileIntentNotice` — machine- and human-readable profile labelling.
- `exportScopeWarnings` — window limits, truncation, and profile-specific lines (`AUDIT_HANDOFF` / `COMPLIANCE_PACK` markers where applicable).

## What is *not* guaranteed

- Chain and sequence flags apply only to **receipts included** in the export (and may be truncated at the row cap).
- “Signature chain valid” style flags are **observed-within-export-order** diagnostics, not a full-register or legal compliance certificate.
- Database-wide integrity (duplicates, orphans, global sequence) is a **different** tool: `GET /api/admin/integrity` / admin **Data integrity** page.

## Audit logging

Successful exports log one of:

- `FiscalExportDiagnostic`
- `FiscalExportAuditHandoff`
- `FiscalExportCompliancePack`

with register id, UTC range, `includeCsv`, receipt/closing counts, and `exportProfile`.

## Backward compatibility

- Omitting `exportProfile` behaves as **`diagnostic`** (same effective permission as before: `report.export`).
- Existing clients that only send `cashRegisterId`, `fromUtc`, `toUtc`, `includeCsv`, `format` continue to receive diagnostic-labelled packages with the new `exportProfile` fields populated.

## References

- Controller: `backend/Controllers/FiscalExportController.cs`
- Rules: `backend/Authorization/FiscalExportProfileRules.cs`
- DTOs: `backend/Models/Export/FiscalExportDtos.cs`, `FiscalExportProfileMetadata.cs`
- Service: `backend/Services/FiscalExportService.cs`
