/**
 * Backup izleme panosu: saf metrik türetimi (API koşularından).
 */
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { RestoreVerificationStatus } from '@/api/generated/model/restoreVerificationStatus';
import { formatRunDurationMs } from '@/features/backup-dr/logic/backupPipelineDerived';

export type MetricStatus = 'success' | 'warning' | 'error' | 'info';

const MS_DAY = 86_400_000;
const THIRTY_DAYS_MS = 30 * MS_DAY;

const TERMINAL_STATUSES = new Set<number>([
  BackupRunStatus.NUMBER_3,
  BackupRunStatus.NUMBER_4,
  BackupRunStatus.NUMBER_5,
  BackupRunStatus.NUMBER_6,
]);

export function mapBackupRunToMetricStatus(
  status: number | undefined,
  options?: { simulated?: boolean; active?: boolean }
): MetricStatus | undefined {
  if (status === undefined) return undefined;
  if (options?.simulated && status === BackupRunStatus.NUMBER_3) return 'warning';
  if (
    status === BackupRunStatus.NUMBER_0 ||
    status === BackupRunStatus.NUMBER_1 ||
    status === BackupRunStatus.NUMBER_2
  ) {
    return options?.active ? 'info' : 'info';
  }
  if (status === BackupRunStatus.NUMBER_3) return 'success';
  if (status === BackupRunStatus.NUMBER_4 || status === BackupRunStatus.NUMBER_5) {
    return 'error';
  }
  if (status === BackupRunStatus.NUMBER_6) return 'warning';
  return 'info';
}

export function mapRestoreDrillToMetricStatus(
  status: number | undefined
): MetricStatus | undefined {
  if (status === undefined) return undefined;
  if (
    status === RestoreVerificationStatus.NUMBER_0 ||
    status === RestoreVerificationStatus.NUMBER_1
  ) {
    return 'info';
  }
  if (status === RestoreVerificationStatus.NUMBER_2) return 'success';
  if (status === RestoreVerificationStatus.NUMBER_3) return 'error';
  return 'info';
}

export function mapConfigurationLevelToMetricStatus(
  level: string | undefined | null
): MetricStatus {
  const n = (level ?? '').trim().toLowerCase();
  if (n === 'healthy') return 'success';
  if (n === 'degraded') return 'warning';
  if (n === 'unhealthy') return 'error';
  return 'info';
}

export interface SuccessRateWindowResult {
  ratePercent: number | null;
  terminalCount: number;
  succeededCount: number;
}

export function computeSuccessRateInWindow(
  runs: readonly BackupRunResponseDto[],
  windowStartMs: number,
  windowEndMs: number
): SuccessRateWindowResult {
  let terminalCount = 0;
  let succeededCount = 0;

  for (const run of runs) {
    const at = run.completedAt ?? run.requestedAt;
    if (!at) continue;
    const ts = Date.parse(at);
    if (Number.isNaN(ts) || ts < windowStartMs || ts >= windowEndMs) continue;
    const st = run.status;
    if (st === undefined || !TERMINAL_STATUSES.has(st)) continue;
    terminalCount += 1;
    if (st === BackupRunStatus.NUMBER_3) succeededCount += 1;
  }

  if (terminalCount === 0) {
    return { ratePercent: null, terminalCount: 0, succeededCount: 0 };
  }
  return {
    ratePercent: Math.round((succeededCount / terminalCount) * 100),
    terminalCount,
    succeededCount,
  };
}

/** Son 30 gün vs önceki 30 gün başarı oranı farkı (yüzde puan). */
export function computeSuccessRateTrendPercent(
  runs: readonly BackupRunResponseDto[],
  nowMs: number = Date.now()
): number | undefined {
  const current = computeSuccessRateInWindow(runs, nowMs - THIRTY_DAYS_MS, nowMs);
  const prior = computeSuccessRateInWindow(
    runs,
    nowMs - 2 * THIRTY_DAYS_MS,
    nowMs - THIRTY_DAYS_MS
  );
  if (current.ratePercent === null || prior.ratePercent === null) return undefined;
  if (prior.terminalCount < 2 && current.terminalCount < 2) return undefined;
  return current.ratePercent - prior.ratePercent;
}

/** Recharts satırı — terminal koşu başına (30 gün penceresi). */
export interface BackupHistory30DayChartRow {
  key: string;
  runId?: string;
  date: string;
  success: number;
  failed: number;
  duration: number;
}

export function buildBackupHistory30DayChartData(
  runs: readonly BackupRunResponseDto[],
  formatDate: (iso: string) => string,
  nowMs: number = Date.now()
): BackupHistory30DayChartRow[] {
  const windowStart = nowMs - THIRTY_DAYS_MS;

  return runs
    .map((run) => {
      const completedAt = run.completedAt ?? run.requestedAt;
      if (!completedAt || run.status === undefined || !TERMINAL_STATUSES.has(run.status)) {
        return null;
      }
      const ts = Date.parse(completedAt);
      if (Number.isNaN(ts) || ts < windowStart || ts > nowMs) return null;

      const st = run.status;
      const startIso = run.startedAt ?? run.requestedAt;
      const durationMs = formatRunDurationMs(startIso, run.completedAt);
      const duration = durationMs !== undefined ? Math.round(durationMs / 1000) : 0;

      return {
        key: run.id ?? completedAt,
        runId: run.id,
        date: formatDate(completedAt),
        success: st === BackupRunStatus.NUMBER_3 ? 1 : 0,
        failed: st === BackupRunStatus.NUMBER_4 || st === BackupRunStatus.NUMBER_5 ? 1 : 0,
        duration,
        ts,
      };
    })
    .filter((row): row is NonNullable<typeof row> => row !== null)
    .sort((a, b) => a.ts - b.ts)
    .map(({ ts: _ts, ...row }) => row);
}

export interface BackupDurationChartPoint {
  key: string;
  label: string;
  durationSec: number;
}

export function buildBackupDurationChartPoints(
  runs: readonly BackupRunResponseDto[],
  formatLabel: (iso: string) => string,
  maxPoints = 14
): BackupDurationChartPoint[] {
  const points = runs
    .filter((r) => r.status === BackupRunStatus.NUMBER_3)
    .map((r) => {
      const ms = formatRunDurationMs(r.requestedAt, r.completedAt);
      const ts = Date.parse(r.completedAt ?? r.requestedAt ?? '');
      return { run: r, ms, ts };
    })
    .filter((x) => x.ms !== undefined && !Number.isNaN(x.ts))
    .sort((a, b) => a.ts - b.ts)
    .slice(-maxPoints);

  return points.map(({ run, ms, ts }) => ({
    key: run.id ?? String(ts),
    label: formatLabel(new Date(ts).toISOString()),
    durationSec: Math.round((ms ?? 0) / 1000),
  }));
}

export function estimateRpoSeconds(
  lastSuccessfulBackupAt: string | undefined | null
): number | undefined {
  if (!lastSuccessfulBackupAt) return undefined;
  const ts = Date.parse(lastSuccessfulBackupAt);
  if (Number.isNaN(ts)) return undefined;
  return Math.max(0, (Date.now() - ts) / 1000);
}

export function estimateRtoSeconds(params: {
  averageSucceededBackupDurationSeconds?: number | null;
  restoreProofAgeSeconds?: number | null;
}): number | undefined {
  const backup = params.averageSucceededBackupDurationSeconds;
  if (backup !== undefined && backup !== null && backup > 0) {
    return backup;
  }
  return undefined;
}
