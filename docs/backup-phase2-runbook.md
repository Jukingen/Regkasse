# Backup Phase 2 — PostgreSQL logical dump (pg_dump)

## Scope

- **In scope:** `ExecutionAdapterKind=PgDump` runs `pg_dump -Fc` from the **backup orchestrator worker** only; artifacts written under `Backup:ArtifactStagingRoot`; optional **on-disk SHA-256** re-hash during artifact verification (still **not** restore proof).
- **Out of scope:** WAL archiving, PITR, `pg_basebackup`, automated restore drills, `pg_verifybackup`, TSE vendor backup (explicit gap).

## Configuration

| Key | Notes |
|-----|--------|
| `ExecutionAdapterKind` | `PgDump` for real logical dumps. |
| `ArtifactStagingRoot` | **Required** for PgDump (directory; created if missing). In non-Development, must be an **absolute** path. |
| `PgDumpExecutablePath` | Default `pg_dump` (PATH). On Windows may be full path to `pg_dump.exe`. |
| `PgDumpTimeoutSeconds` | Minimum 60 enforced in adapter. |
| `LogicalDumpConnectionStringName` | Connection string key; default uses `DefaultConnection` if unset. Prefer a **least-privilege backup role** in production (assumption: DBA provisions role with rights sufficient for `pg_dump`). |
| `VerifyLogicalDumpFileOnDisk` | When true, verifier re-hashes files marked `RequireOnDiskHashVerification` and ensures paths stay under `ArtifactStagingRoot`. **Required true in non-Development when using PgDump** (startup validation). |

## Startup validation (non-Development + PgDump)

The host fails `ValidateOnStart` when:

- Named logical-dump connection string is missing or not parseable as Npgsql, or lacks Host / Username / Database.
- `VerifyLogicalDumpFileOnDisk` is false (metadata-only success would otherwise be allowed).
- `ArtifactStagingRoot` is not an absolute path.
- `ExternalArchiveRoot` is missing or not absolute (required so every successful PgDump run copies to secondary storage with post-copy SHA-256).

In Development, missing `ExternalArchiveRoot` yields **Degraded** configuration health (runs can still succeed with staging-only verification).

## Artifact pipeline (Phase 3)

After staging on-disk verification, the worker copies artifacts to `ExternalArchiveRoot/{runId:N}/` and re-hashes each destination file. API responses expose `StorageLocator` / `ExternalRedactedLocator` only (no host paths).

## Security

- Password is passed via subprocess `PGPASSWORD` (not argv). Do not log connection strings or stderr containing secrets.
- Staging paths are guarded against directory traversal (`BackupPathGuard`).

## Operational checklist

1. Install PostgreSQL client tools on the API host (or sidecar) so `pg_dump` is available.
2. Set `ArtifactStagingRoot` to a dedicated volume with enough space.
3. Configure connection string (dedicated backup user recommended).
4. Non-Development: do **not** use `Fake`. Use `PgDump` or acknowledged `ProductionStub`.
5. After first run: confirm `backup_runs` → `Succeeded`, files on disk, and admin `configurationHealth` is not `Unhealthy`.

## TSE / RKSV

Database logical dumps do **not** replace TSE device / certificate backup procedures. Treat TSE as a **separate operator/vendor runbook**.

## Legal hold / retention

This phase does not delete fiscal data. Retention of files on `ArtifactStagingRoot` is an operational policy; align with legal hold and audit retention policies without deleting protected audit evidence.
