import type { BackupArtifactResponseDto } from "@/api/generated/model";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import type { BackupRunResponseDto } from "@/api/generated/model";
import { formatBackupBytes } from "@/features/backup-dr/logic/backupFormat";

export type BackupRunStatusUiKey =
  | "queued"
  | "running"
  | "awaitingVerification"
  | "succeeded"
  | "failed"
  | "verificationFailed"
  | "cancelled"
  | "unknown";

export function resolveBackupRunStatusUiKey(
  status: number | undefined,
): BackupRunStatusUiKey {
  switch (status) {
    case BackupRunStatus.NUMBER_0:
      return "queued";
    case BackupRunStatus.NUMBER_1:
      return "running";
    case BackupRunStatus.NUMBER_2:
      return "awaitingVerification";
    case BackupRunStatus.NUMBER_3:
      return "succeeded";
    case BackupRunStatus.NUMBER_4:
      return "failed";
    case BackupRunStatus.NUMBER_5:
      return "verificationFailed";
    case BackupRunStatus.NUMBER_6:
      return "cancelled";
    default:
      return "unknown";
  }
}

export function computeBackupRunDurationMinutes(
  startedAt: string | null | undefined,
  completedAt: string | null | undefined,
): number | undefined {
  if (!startedAt || !completedAt) return undefined;
  const start = Date.parse(startedAt);
  const end = Date.parse(completedAt);
  if (Number.isNaN(start) || Number.isNaN(end) || end < start) return undefined;
  return (end - start) / 60_000;
}

export function sumArtifactBytes(
  artifacts: BackupArtifactResponseDto[] | null | undefined,
): number {
  if (!artifacts?.length) return 0;
  return artifacts.reduce((sum, a) => sum + (a.byteSize ?? 0), 0);
}

export function resolveBackupRunTotalBytes(record: BackupRunResponseDto): number {
  return record.totalSizeBytes ?? sumArtifactBytes(record.artifacts);
}

export function resolveBackupRunDurationLabel(
  record: BackupRunResponseDto,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  if (record.durationFormatted?.trim()) return record.durationFormatted.trim();
  const minutes = computeBackupRunDurationMinutes(record.startedAt, record.completedAt);
  if (minutes === undefined) return t("backupDr.runsTable.noDuration");
  return t("backupDr.runsTable.durationMinutes", { minutes: minutes.toFixed(1) });
}

export function resolveBackupRunSizeLabel(
  record: BackupRunResponseDto,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  if (record.totalSizeFormatted?.trim()) return record.totalSizeFormatted.trim();
  const total = resolveBackupRunTotalBytes(record);
  return formatBackupBytes(total > 0 ? total : undefined, t);
}

export function isBackupRunFailed(status: number | undefined): boolean {
  return (
    status === BackupRunStatus.NUMBER_4 ||
    status === BackupRunStatus.NUMBER_5
  );
}

export function compareBackupRunsByRequestedAtDesc(
  a: BackupRunResponseDto,
  b: BackupRunResponseDto,
): number {
  const ta = a.requestedAt ? Date.parse(a.requestedAt) : 0;
  const tb = b.requestedAt ? Date.parse(b.requestedAt) : 0;
  return tb - ta;
}

/**
 * Client-side hint filter when Super Admin selects a tenant (idempotency key encodes tenant on manual trigger).
 * Scheduled/automatic runs without tenant prefix remain visible when no filter is set.
 */
export function filterBackupRunsByTenantIdempotency(
  runs: BackupRunResponseDto[],
  tenantId: string | undefined,
): BackupRunResponseDto[] {
  const id = tenantId?.trim();
  if (!id) return runs;
  const needle = `manual-tenant-${id}`.toLowerCase();
  return runs.filter((run) => (run.idempotencyKey ?? "").toLowerCase().includes(needle));
}
