# Fiscal export — terminology guide (diagnostic vs guarantee)

Short reference for export/integrity wording so "diagnostic" and "guarantee" are clearly separated.

## Terms we use

| Term | Meaning | Use when |
|------|---------|----------|
| **diagnostic** | Best-effort, for ops/support interpretation; not a legal or global guarantee. | Describing `integrity.*` booleans, chain/sequence checks, and any flag that is scope-limited. |
| **observed-within-scope** | The check applies only to the data included in this export (e.g. receipts in the UTC window); nothing outside the slice is validated. | Explaining why a "true" flag does not prove global validity. |
| **best-effort** | We compute what we can from the export slice; we do not guarantee completeness or that the result holds for the full register/history. | Describing integrity logic and export scope. |
| **warning / not-proof** | The export is not RKSV compliance proof; consumers must read `exportScopeWarnings` and `integrityDiagnosticNotes`. | In docs and in payload text (e.g. first exportScopeWarning). |

## Terms we avoid for export flags

- **Valid** (without qualifier) → Prefer "diagnostic: ... observed-within-scope" or "true when ... in this export only; not proof of full chain."
- **Continuous** (without qualifier) → Prefer "diagnostic: ... in export order only; not a full register audit."
- **Guarantee / proof / attestation** → Do not use for integrity booleans; reserve for explicit legal/certification contexts if ever introduced.

## API contract

Property names (`SignatureChainValid`, `SequenceContinuous`, etc.) are **unchanged** for backward compatibility. Semantics are clarified via:

- Doc comments (XML summary) on DTOs and service.
- `exportScopeWarnings` and `integrityDiagnosticNotes` in the JSON payload.
- This guide and `FISCAL_EXPORT_DIAGNOSTICS.md`.

## Where it applies

- **Fiscal export** (`GET /api/admin/fiscal-export`): `FiscalExportPackageDto`, `FiscalExportIntegrityDto`, `ChainContinuityWarnings`, and related release/docs.
- **Integrity check** (e.g. `GET /api/admin/integrity`): separate feature; use "integrity" only in the sense of internal consistency checks, not as RKSV proof.
