# Real PostgreSQL logical backups in local / Development

This document explains how to run **`pg_dump` custom-format (`-Fc`)** backups in **Development** instead of the default **`Fake`** adapter (stub bytes only).

Canonical implementation references:

- Adapter selection: `Services/Backup/BackupOrchestratorHostedService.cs` (`SelectAdapter`)
- Real dump: `Services/Backup/PostgreSqlPgDumpBackupExecutionAdapter.cs`
- Fake stub: `Services/Backup/FakeBackupExecutionAdapter.cs`
- Config evaluation: `Services/Backup/BackupConfigurationEvaluation.cs`
- Restore drill (`pg_restore --list`): `Services/RestoreVerification/PgRestoreListInspector.cs`

---

## Admin UI (Backup & DR dashboard)

The admin **Backup & disaster recovery** page shows:

- **Scope / intro alert:** short note that default Development often uses **Fake** (intentional), with a **jump link** to the developer checklist on the same page and the **repository path** to this document (`backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md`) for copy/paste in the checkout.
- **Fake adapter:** an informational (blue) banner explaining stub behavior — expected and safe in default dev, not an error — plus prerequisites for real dumps (PgDump, staging, connection string, client binaries) and the same checklist/doc hints.
- **Configuration health card:** when the API returns `diagnostics`, **stable machine codes** (`BACKUP_SETUP_*`) appear as tags (hover for English detail) alongside issue text — useful to see why real pg_dump is inactive before the first backup run.
- **Summary card (Fake):** the Fake/Stub footnote has a **tooltip** with the minimum settings and on-disk filename pattern.
- **Developer checklist** card (anchor id `backup-dr-dev-pgdump-checklist`): same minimum settings as the tables below, plus troubleshooting rows and a copyable doc path.
- **PgDump with real logical dump configured:** a green banner confirming that `pg_restore --list` / restore drills can target a real custom-format archive (still validate retention and off-site copies operationally).

Strings are translated (de / en / tr); technical setting names stay in English in copy.

---

## RealPgDump **selectable** in the admin UI (without changing repo default adapter)

The Backup & DR **execution mode** card enables **RealPgDump** only when hypothetical PgDump configuration health is **not Unhealthy** (see `BackupExecutionModeApiMapper.BuildSelectableModes`). The repo intentionally keeps `Backup:ExecutionAdapterKind` = **Fake**; you do **not** need to set `ExecutionAdapterKind` = `PgDump` in config to make the radio option selectable — that would also risk startup validation ordering issues if other prerequisites were missing.

**Minimum local setup (user secrets or env — do not commit paths):**

1. Set absolute paths for staging and (recommended) external archive so hypothetical evaluation clears the usual blockers:

   ```bash
   cd backend
   dotnet user-secrets set "Backup:ArtifactStagingRoot" "C:\data\regkasse-backup-staging"
   dotnet user-secrets set "Backup:ExternalArchiveRoot" "C:\data\regkasse-backup-archive"
   ```

   Or on Windows run `scripts/setup-backup-dr-dev-secrets.ps1` from `backend/` (same defaults).

2. Ensure `ConnectionStrings:DefaultConnection` is valid Npgsql (Host, Username, Database) — already required for the API to start.

3. Restart the API. In **Development**, `Backup:VerifyLogicalDumpFileOnDisk` defaults to **true** (no change needed). Set `Backup:PgDumpExecutablePath` only if `pg_dump` is not on `PATH`.

4. Open the admin Backup & DR page and select **RealPgDump** there when ready; the effective adapter remains **Fake** until you save that mode.

**Verify:** `GET /api/admin/backup/execution-mode` — `hypotheticalPgDumpHealthLevel` should not be `Unhealthy`, and `selectableModes` should include `userFacingMode: "RealPgDump"` with `selectable: true`. `effectiveConfigurationHealth` may still reflect **Fake** until you persist Real mode.

---

## Current state

| Area | Default (repo + `appsettings.json`) | Notes |
|------|--------------------------------------|--------|
| **Adapter** | `Backup:ExecutionAdapterKind` = **`Fake`** | Safe default; no `pg_dump`, produces `fake-bytes-…` and JSON metadata. |
| **Staging** | `ArtifactStagingRoot` = **null** | Fake can write under OS temp; **PgDump requires a non-empty staging root**. |
| **Connection** | `LogicalDumpConnectionStringName` often null → adapter uses **`DefaultConnection`** | For PgDump, the named connection string must exist and point at the DB to dump. |
| **pg_dump / pg_restore** | Not bundled by the API | Must be installed and on the **PATH**, or paths set explicitly (see below). |
| **Intentional block in Development?** | **No** | There is **no** code path that forbids `PgDump` when `ASPNETCORE_ENVIRONMENT=Development`. Production-like **extra** checks (absolute staging, external archive, connection validation in `BackupConfigurationEvaluation`) are **skipped or relaxed** in Development. |

---

## Required environment variables / settings

Minimum to **run** real `pg_dump` (effective adapter PgDump):

| Setting | Value | Purpose |
|---------|--------|---------|
| `Backup__ExecutionAdapterKind` | `PgDump` | Selects `PostgreSqlPgDumpBackupExecutionAdapter` (or persist **RealPgDump** in admin execution mode). |
| `Backup__ArtifactStagingRoot` | Absolute path, e.g. `C:\data\regkasse-backup-staging` or `/var/regkasse-backup-staging` | Output directory for `.dump` + manifest; **required** for PgDump. |
| `ConnectionStrings__DefaultConnection` | Valid Npgsql connection string | Or set `Backup__LogicalDumpConnectionStringName` to another name and define that connection string. |

To make **RealPgDump selectable** in the admin UI while keeping the default **Fake** adapter, set only `Backup__ArtifactStagingRoot` (and recommended `Backup__ExternalArchiveRoot`) via user secrets; do **not** set `ExecutionAdapterKind` in config unless you intentionally want the config default to be PgDump.

Optional but recommended for restore drill verification on the same machine:

| Setting | Value | Purpose |
|---------|--------|---------|
| `RestoreVerification__PgRestoreExecutablePath` | e.g. `C:\Program Files\PostgreSQL\16\bin\pg_restore.exe` | If `pg_restore` is not on `PATH`. |
| `Backup__PgDumpExecutablePath` | e.g. `…\pg_dump.exe` | If `pg_dump` is not on `PATH`. |

Optional in Development (otherwise health may show **Degraded**):

| Setting | Value | Purpose |
|---------|--------|---------|
| `Backup__ExternalArchiveRoot` | Absolute path or omit | In Development, external archive is **optional**; omitting triggers a **degraded** readiness note, not failure. |

**Ways to apply:** `appsettings.Development.json` (local only, not committed with secrets), **User Secrets**, or environment variables as above.

Example **User Secrets** (CLI) from `backend/` (project already has `UserSecretsId`):

```bash
# Paths only — enables RealPgDump in admin UI; keeps ExecutionAdapterKind=Fake from appsettings
dotnet user-secrets set "Backup:ArtifactStagingRoot" "C:\data\regkasse-backup-staging"
dotnet user-secrets set "Backup:ExternalArchiveRoot" "C:\data\regkasse-backup-archive"

# Optional: make PgDump the config default (not required for selectability)
# dotnet user-secrets set "Backup:ExecutionAdapterKind" "PgDump"
```

---

## Required binaries / local dependencies

| Binary | Role | Typical install |
|--------|------|------------------|
| **`pg_dump`** | Emits **custom-format** (`-Fc`) logical dump | PostgreSQL client tools (same major version as server recommended). |
| **`pg_restore`** | Used by restore drill for **`pg_restore --list`** (and optional isolated restore) | Same as above. |

The API process must be able to **start** these executables (PATH or `PgDumpExecutablePath` / `RestoreVerification:PgRestoreExecutablePath`).

---

## Expected output path on disk

With `ArtifactStagingRoot = <ROOT>` (absolute, directory created if missing):

| File | Pattern |
|------|---------|
| Logical dump | `<ROOT>\logical_{BackupRunId:N}_{yyyyMMddHHmmss}Z.dump` |
| Manifest | `<ROOT>\manifest_{BackupRunId:N}.json` |

Implementation: `PostgreSqlPgDumpBackupExecutionAdapter.ExecuteAsync`.

If you previously used **Fake** with no staging, files could appear under `%TEMP%\regkasse-backup-stub\` — that path is **not** used by PgDump.

---

## Expected artifact types for a valid real run

Database rows (`BackupArtifact`):

- **`LogicalDump`** — custom-format file; storage descriptor is the **relative** filename under staging.
- **`VerificationManifest`** — JSON manifest beside the dump.

On-disk sanity after `pg_dump`: header must start with **`PGDMP`** (`PgDumpCustomFormatSanity`).

---

## How to verify locally that the backup is real

1. **Configuration / admin UI:** Effective adapter should show **PgDump** (not Fake); `realPostgreSqlLogicalDumpConfigured` / health narrative should reflect real dump path when evaluated with connection string present.
2. **Filesystem:** Open the `.dump` file in a hex editor or `Format-Hex` / `xxd` — first bytes should be **`PGDMP`** (PostgreSQL custom archive magic).
3. **CLI:**  
   `pg_restore --list "<ROOT>\logical_....dump"`  
   Should exit **0** and print TOC lines (not stderr about invalid format).
4. **Size:** Stub Fake files are tiny text; a real DB dump is typically much larger (unless empty DB).

---

## How to verify locally that the restore drill is working

1. Ensure **`pg_restore`** is available (same as for `--list`).
2. Trigger a **restore verification drill** after a **successful PgDump** backup run.
3. **Expected:** `PG_RESTORE_LIST_FAILED` should **not** occur for a valid custom-format file; `pgRestoreListPassed` / dump inspection tri-state should pass the list step.
4. If `IsolatedPgRestoreEnabled` is **false** (default in many configs), the drill may still **skip** actual restore to a clone DB — check `RestoreAttemptSkipReason` / UI. The **list** step is the first gate.

---

## Safety notes for local testing

- Use a **non-production** database connection string for Development.
- Staging and optional external archive directories should be **writable** by the OS user running the API/worker.
- **Do not** commit real connection strings or machine-specific paths to the repo; use User Secrets or local `appsettings.Development.json` (gitignored if your team uses that pattern).

---

## Troubleshooting (where developers get stuck)

| Symptom | Likely cause | What to do |
|--------|----------------|----------|
| Admin **Backup readiness** stays degraded; issues mention **Fake** | `Backup:ExecutionAdapterKind` still **Fake** (repo default) | Set `PgDump` via env / User Secrets / `appsettings.Development.json`. Restart API. |
| **Unhealthy** / validator: **ArtifactStagingRoot** | PgDump selected but staging path empty | Set `Backup:ArtifactStagingRoot` to an **absolute** directory; API user must be able to create/write it. |
| **Degraded**: connection string **Development:** … | `ConnectionStrings:{name}` missing or invalid for `Backup:LogicalDumpConnectionStringName` (default `DefaultConnection`) | Add a valid Npgsql connection string with **Host**, **Username**, **Database** (and password if needed). After this change, `BackupConfigurationEvaluation` surfaces the issue in **admin backup status** (not only at first backup run). |
| Backup run fails with **PG_DUMP_FAILED** / process error | **`pg_dump` not on PATH** or wrong binary | Install PostgreSQL client tools, or set `Backup:PgDumpExecutablePath` to the full `pg_dump` / `pg_dump.exe` path. |
| Restore drill fails **PG_RESTORE_LIST_FAILED** | **`pg_restore` not on PATH** | Same as above for `pg_restore`, or `RestoreVerification:PgRestoreExecutablePath`. |
| Startup log: **Backup development tooling: pg_dump … failed** | Same as pg_dump PATH | Fix PATH or explicit executable path; see `BackupStartup` / `Backup development tooling` log lines after `=== KASSE API STARTED ===`. |

### Config evaluation vs first run

- **Connection string** checks for PgDump now run in **Development** as well (as **Degraded** issues), so missing `DefaultConnection` is visible in the admin Backup & DR **configuration health** block before you trigger a job.
- **Binary presence** is probed at startup (`pg_dump --version` / `pg_restore --version`) only when `ASPNETCORE_ENVIRONMENT=Development` and `ExecutionAdapterKind=PgDump`; failures are **warnings** in logs, not a hard startup failure (so you can still boot the API without PostgreSQL tools on machines that only use Fake).

---

## How to read startup logs (quick)

After the API prints `=== KASSE API STARTED ===`, look for:

1. **`Backup orchestration:`** — `health`, `adapterKind`, `realPostgreSqlLogicalDump`, and `issues` (if not Healthy).
2. **`Backup development tooling:`** — only in Development with PgDump: confirms `pg_dump` / `pg_restore` `--version` succeeded or logs the fix hint (`Backup:PgDumpExecutablePath` / `RestoreVerification:PgRestoreExecutablePath`).
