# Restore boundary — notes

**Hub:** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)  
**Always-applied:** [`AGENTS.md`](../AGENTS.md) § Backup & Disaster Recovery.  
**RKSV restore rules:** [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md).

## Current state (API control plane)

| Surface | What it does | Production write? |
|---------|--------------|-------------------|
| `DeferredRestoreOrchestrationBoundary` | Capability metadata only (`DescribeCapabilities`); automated restore **not** available | No |
| `GET /api/admin/backup/status/latest` → `restore` | Reports that automated restore is unavailable | No |
| `POST /api/admin/restore/*` (`AdminRestoreController`) | Super Admin **validation-only** manual restore with dual approval | No — isolated `restore_validation_*` DB only |
| `GET /api/admin/restore/compliance-check` | Pre-restore same-tenant + integrity + RKSV gates | No — read-only |
| `GET /api/admin/restore/request/{id}/report` | RKSV-oriented compliance report (`IRestoreReportService`) | No — read-only evidence |
| `POST /api/admin/restore-verification/*` | Restore **drills** (inspect / optional isolated `pg_restore` / fiscal SQL / smoke) | No — ephemeral or non-production targets |

### Manual restore (RKSV-oriented guards)

- **Compliance gate:** `IRestoreService` / `RestoreService` — same-tenant when ambient tenant and `backup_runs.tenant_id` are both set; rejects non-validation paths; does **not** run production restore.
- **Content / cost policy:** `docs/BACKUP_CONTENT_POLICY.md` (Tenant vs System; Identity only in System; retention 30/90; staging disk 80% alert; `-Fc -Z` compression on System dumps).
- **Artifacts:**
  - **System** `pg_dump` → validation restore / drills.
  - **Tenant** `*.tenant.zip` → **not** `pg_restore`-compatible (`TENANT_PACKAGE_RESTORE_NOT_SUPPORTED`); export/archive for Mandanten-Admin, not validation restore input.
- **Same-tenant gate:** when ambient tenant and labeled `backup_runs.tenant_id` disagree → **404**. Cross-tenant production restore via API is forbidden by design.
- **Who may restore:** Super Admin only. Mandanten-Admin may view/download own Tenant packages but **cannot** request or approve restore.
- **Audit:** every request/approve/reject/complete/fail writes `AuditEventType.Restore*` with correlation id, restore timestamps (`RequestedAt` / `ApprovedAt` / `AuditRecordedAtUtc`), `SourceBackupTenantId`, and `RestoreScope` (`tenant_access_scoped` | `deployment_wide`). Audit `tenant_id` is stamped from the source backup tenant when set; otherwise `LegacyDefaultTenantIds.Primary` (platform convention).
- **No backdating:** the workflow does not rewrite fiscal `IssuedAt` / receipt timestamps; it only restores into an isolated clone for validation.
- **No silent production modification:** `ManualRestoreTargetDatabaseGuard` + `ValidationRestoreExecutionService` block `DefaultConnection` and require ValidationOnly.

### Drill evidence

- L4 continuity SQL (`PostRestoreDrillSqlChecker`) and optional `fiscal_go_live_validation.sql` exist.
- Full TSE signature-chain gap detection as a hard restore gate is **not** guaranteed in all configs; see `docs/restore-verification-drill-runbook.md`.

## Operator-led production recovery

Treat live recovery as **manual PostgreSQL operations** outside this API (infrastructure backups / DBA runbook). Use `backup_runs` / `backup_artifacts` as evidence pointers, not as a guarantee that files exist on disk (especially for `Fake` adapter). Prefer **System** dumps for instance recovery; Tenant ZIPs are structured exports, not drop-in `pg_restore` inputs.

## What Phase 2+ should still add

1. Optional **filtered tenant restore** product path (apply Tenant ZIP into an isolated tenant-scoped validation DB) so Mandanten-Admin DR does not depend solely on System dumps.
2. Deeper verification (`pg_verifybackup` or vendor-equivalent) kept separate from artifact metadata verification.
3. RPO/RTO tooling tied to WAL archive lag and last verified backup.
4. Coordinated TSE cryptographic material restore (vendor-specific; separate from PostgreSQL).

## Fiscal integrity reminder

After any operator-led restore, planned checks include receipt chain continuity, `signature_chain_state`, receipt sequence allocation, FinanzOnline outbox idempotency, and audit log continuity — performed as **post-restore validation procedures**. Do not replay FinanzOnline outbox blindly after restore.
