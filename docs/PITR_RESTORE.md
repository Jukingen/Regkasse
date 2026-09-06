# Point-in-Time Recovery (PITR) and incremental backups

**Audience:** Operators, Super Admin, backend/FA maintainers.  
**Hub:** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)  
**Restore boundary:** [`restore-boundary-notes.md`](restore-boundary-notes.md)

## What this product does

| Capability | Status |
|------------|--------|
| Daily Tenant incremental ZIP (delta after last full Tenant backup) | Opt-in (`Backup:IncrementalBackupEnabled`) |
| Incremental restore **plan** (full + later incrementals) | API + FA chain view |
| Isolated dry-run of the nearest **System** `pg_dump` | Super Admin only |
| WAL file inventory + 7-day rotation | When Postgres `archive_command` writes to `Backup:WalArchiveDirectory` |
| Pre-restore report (hash, schema/content, TSE checks) | Uses existing checksum + content-validation services |
| Production restore / automatic WAL replay onto live DB | **Not supported** |

Tenant incremental packages (`*.tenant.incr.zip`) are **not** `pg_restore` input. Isolated validation restore always uses a System logical dump. WAL replay after that dump is a **host DBA** procedure.

## PostgreSQL WAL archiving (required for true PITR)

The API cannot turn on `archive_mode`. Configure the database host:

```conf
wal_level = replica
archive_mode = on
archive_timeout = 300
archive_command = 'test ! -f /var/lib/postgresql/wal-archive/%f && cp %p /var/lib/postgresql/wal-archive/%f'
```

App settings:

| Key | Default | Purpose |
|-----|---------|---------|
| `Backup:WalArchiveDirectory` | `{ContentRoot}/App_Data/wal-archive` | Directory the API inventories / rotates |
| `Backup:WalArchiveRetentionDays` | `7` | Delete archived segments older than this |
| `Backup:WalArchiveSwitchIntervalMinutes` | `5` | Planner lag + recommended `archive_timeout` |
| `Backup:PitrWalArchivingDeclaredEnabled` | `false` | Planning flag when files are not visible to the API yet |
| `Backup:IncrementalBackupEnabled` | `false` | Daily Tenant incremental enqueue |
| `Backup:IncrementalBackupCron` | `0 3 * * *` | After the typical 02:00 UTC System full |

`WalArchiveRetentionHostedService` deletes expired files hourly. Point `archive_command` at the same directory the API reads.

## Restore procedure (validation only)

1. Super Admin opens FA `/backup/pitr`.
2. Confirm WAL status and the backup chain (full + incrementals + System dumps).
3. Pick a UTC date/time. The API selects the nearest succeeded backup at or before that time and estimates data loss (seconds after that backup, minus WAL coverage when files exist).
4. Pre-restore validation runs:
   - SHA-256 artifact re-hash (`IBackupChecksumVerificationService`)
   - Schema/content + TSE chain (`IBackupContentValidationService`)
5. **Dry-run** enqueues an isolated restore drill of the nearest System dump (`restore_validation_*` / `rv_v_*`). Production is not modified.
6. Review the drill report. Operator-led live recovery (including `pg_basebackup` + WAL replay) stays outside the API.

Dual Super Admin approval still applies to manual restore requests (`POST /api/admin/restore/request`).

## APIs

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/api/admin/backup/pitr/availability` | `settings.view` | Super Admin may omit ambient tenant |
| POST | `/api/admin/backup/pitr/validate` | `settings.view` | Restore-point plan |
| GET | `/api/admin/backup/pitr/wal` | `settings.view` | Archive inventory |
| GET | `/api/admin/backup/pitr/chain` | `settings.view` | Manager: own Tenant chain |
| POST | `/api/admin/backup/pitr/pre-restore-validate` | `settings.view` | Hash / schema / TSE |
| POST | `/api/admin/backup/pitr/dry-run` | Super Admin + `settings.manage` | Isolated drill |
| POST | `/api/admin/backup/incremental` | `backup.manage` | Enqueue Tenant incremental |
| POST | `/api/admin/backup/incremental/restore` | Super Admin | Plan + isolated System dry-run |

## Test regularly

```bash
dotnet test backend/KasseAPI_Final.sln --filter "PitrServiceTests|WalArchiveServiceTests|BackupChainServiceTests|IncrementalBackupServiceTests"
```

Schedule a Super Admin dry-run after each successful System backup in Staging. Confirm WAL files appear in the archive directory within `archive_timeout`.
