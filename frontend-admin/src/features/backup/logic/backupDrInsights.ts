import type { RestoreVerificationRunDto } from '@/features/backup/logic/restoreVerificationApi';

const DAY_MS = 24 * 60 * 60 * 1000;
/** Documented worker default. The list API does not return the deployment override. */
export const DEFAULT_DRILL_CADENCE_DAYS = 7;

export type DrillResult = 'Succeeded' | 'Failed' | 'Pending';
export type GateColor = 'green' | 'yellow' | 'red';

export type DrillTimelineItem = {
  id: string;
  at: string;
  durationMs: number | null;
  result: DrillResult;
  reason: string;
  /** Passed and Failed drills can download a PDF. Cancelled and pending drills cannot. */
  reportable: boolean;
};

const SENSITIVE = /(\\|\/|password|secret|credential|connectionstring|postgres:\/\/|host=)/i;

/** Hide paths, connection strings, and credential-like text before it reaches the UI. */
export function maskSensitiveOperatorText(value: string | null | undefined): string {
  const text = (value ?? '').trim();
  if (!text) return '';
  if (SENSITIVE.test(text)) return '***';
  return text.length > 180 ? `${text.slice(0, 177)}…` : text;
}

function drillResult(status: RestoreVerificationRunDto['status']): DrillResult {
  if (status === 'Succeeded' || status === 2) return 'Succeeded';
  if (status === 'Failed' || status === 3) return 'Failed';
  return 'Pending';
}

function isScheduledTrigger(source: RestoreVerificationRunDto['triggerSource'] | undefined): boolean {
  return source === 'Scheduled' || source === 1;
}

/**
 * CompletedAt of the newest scheduled success in a loaded page.
 * Same rule as recoverability lastSuccessfulRestoreProofAt: manual rows do not count.
 * The uncapped value is that summary field; this helper only covers the loaded page.
 */
export function lastScheduledDrillCompletedAt(
  runs: readonly RestoreVerificationRunDto[]
): string | null {
  let best: string | null = null;
  let bestMs = Number.NEGATIVE_INFINITY;
  for (const run of runs) {
    if (!isScheduledTrigger(run.triggerSource)) continue;
    if (drillResult(run.status) !== 'Succeeded' || !run.completedAt) continue;
    const ms = new Date(run.completedAt).getTime();
    if (!Number.isFinite(ms) || ms <= bestMs) continue;
    bestMs = ms;
    best = run.completedAt;
  }
  return best;
}

function durationMs(run: RestoreVerificationRunDto): number | null {
  if (typeof run.durationMs === 'number' && Number.isFinite(run.durationMs) && run.durationMs >= 0) {
    return run.durationMs;
  }
  if (!run.startedAt || !run.completedAt) return null;
  const ms = new Date(run.completedAt).getTime() - new Date(run.startedAt).getTime();
  return Number.isFinite(ms) && ms >= 0 ? ms : null;
}

export function toDrillTimeline(runs: readonly RestoreVerificationRunDto[]): DrillTimelineItem[] {
  return [...runs]
    .map((run) => {
      const result = drillResult(run.status);
      const rawReason =
        result === 'Failed'
          ? run.failureCode || run.failureDetail || run.restoreAttemptSkipReason
          : run.restoreAttemptSkipReason || run.failureCode;
      const cancelled = (run.failureCode ?? '').toUpperCase() === 'CANCELLED';
      return {
        id: run.id,
        at: run.completedAt || run.requestedAt,
        durationMs: durationMs(run),
        result,
        reason: maskSensitiveOperatorText(rawReason),
        reportable: !cancelled && (result === 'Succeeded' || result === 'Failed'),
      };
    })
    .filter((item) => item.at)
    .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime());
}

export function drillSuccessRateLast30Days(
  items: readonly DrillTimelineItem[],
  now = new Date()
): { rate: number | null; succeeded: number; failed: number } {
  const since = now.getTime() - 30 * DAY_MS;
  const window = items.filter((item) => {
    const at = new Date(item.at).getTime();
    return Number.isFinite(at) && at >= since && item.result !== 'Pending';
  });
  const succeeded = window.filter((item) => item.result === 'Succeeded').length;
  const failed = window.filter((item) => item.result === 'Failed').length;
  const total = succeeded + failed;
  return { rate: total === 0 ? null : Math.round((succeeded / total) * 100), succeeded, failed };
}

export function lastSuccessfulDrill(
  items: readonly DrillTimelineItem[],
  now = new Date()
): { at: string | null; daysSince: number | null } {
  const hit = items.find((item) => item.result === 'Succeeded');
  if (!hit) return { at: null, daysSince: null };
  const age = now.getTime() - new Date(hit.at).getTime();
  return { at: hit.at, daysSince: Number.isFinite(age) ? Math.max(0, Math.floor(age / DAY_MS)) : null };
}

export function drillSchedulePreview(
  lastSuccessAt: string | null,
  now = new Date(),
  cadenceDays = DEFAULT_DRILL_CADENCE_DAYS
): { nextAt: string | null; overdue: boolean } {
  if (!lastSuccessAt) return { nextAt: null, overdue: true };
  const next = new Date(new Date(lastSuccessAt).getTime() + cadenceDays * DAY_MS);
  if (!Number.isFinite(next.getTime())) return { nextAt: null, overdue: true };
  return { nextAt: next.toISOString(), overdue: next.getTime() < now.getTime() };
}

export type CostTrendPoint = { date: string; costEur: number };

function isSucceededBackup(status: unknown): boolean {
  return status === 'Succeeded' || status === 3;
}

/** Indicative € of succeeded dumps created per day. Not a storage invoice. */
export function buildDumpCreationCostTrend(
  runs: readonly { requestedAt?: string | null; status?: unknown; totalSizeBytes?: number | null }[],
  costPerGbEur: number,
  now = new Date()
): CostTrendPoint[] {
  const rate = Number.isFinite(costPerGbEur) && costPerGbEur > 0 ? costPerGbEur : 0;
  const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  start.setUTCDate(start.getUTCDate() - 89);
  const buckets = new Map<string, number>();
  for (let i = 0; i < 90; i += 1) {
    const day = new Date(start);
    day.setUTCDate(start.getUTCDate() + i);
    buckets.set(day.toISOString().slice(0, 10), 0);
  }
  for (const run of runs) {
    if (!isSucceededBackup(run.status) || !run.requestedAt) continue;
    const day = run.requestedAt.slice(0, 10);
    if (!buckets.has(day)) continue;
    const bytes = typeof run.totalSizeBytes === 'number' ? run.totalSizeBytes : 0;
    const gb = bytes / (1024 * 1024 * 1024);
    buckets.set(day, (buckets.get(day) ?? 0) + gb * rate);
  }
  return [...buckets.entries()].map(([date, costEur]) => ({
    date,
    costEur: Math.round(costEur * 10000) / 10000,
  }));
}

/**
 * Integer shares of a cost (or size) breakdown. Largest remainder keeps the parts at exactly 100
 * when any weight is positive; independent Math.round does not.
 */
export function costSharePercents(values: readonly number[]): number[] {
  const weights = values.map((value) => (Number.isFinite(value) && value > 0 ? value : 0));
  const total = weights.reduce((sum, value) => sum + value, 0);
  if (total <= 0) return weights.map(() => 0);

  const exact = weights.map((value) => (value / total) * 100);
  const floored = exact.map((value) => Math.floor(value));
  let remainder = 100 - floored.reduce((sum, value) => sum + value, 0);
  const order = exact
    .map((value, index) => ({ index, fraction: value - Math.floor(value) }))
    .sort((a, b) => b.fraction - a.fraction || a.index - b.index);

  const shares = [...floored];
  for (const entry of order) {
    if (remainder <= 0) break;
    shares[entry.index] += 1;
    remainder -= 1;
  }
  return shares;
}

export function retentionImpact(monthlyCostEur: number, retentionSavingsPercent: number) {
  const actual = Number.isFinite(monthlyCostEur) ? monthlyCostEur : 0;
  const saved = Math.min(99.9, Math.max(0, Number.isFinite(retentionSavingsPercent) ? retentionSavingsPercent : 0));
  const allHot = saved <= 0 ? actual : actual / (1 - saved / 100);
  return {
    actual,
    allHot: Math.round(allHot * 100) / 100,
    savingsPercent: Math.round(saved * 10) / 10,
  };
}

export function systemBackupGate(at: string | null, simulated: boolean, now = new Date()): GateColor {
  if (!at) return 'red';
  const age = now.getTime() - new Date(at).getTime();
  if (!Number.isFinite(age) || age > 7 * DAY_MS) return 'red';
  if (simulated || age > 2 * DAY_MS) return 'yellow';
  return 'green';
}

export function legalHoldGate(
  hold: { legalHold?: boolean | null; legalHoldUntilUtc?: string | null } | null,
  now = new Date()
): GateColor {
  if (!hold?.legalHold) return 'red';
  if (!hold.legalHoldUntilUtc) return 'yellow';
  const until = new Date(hold.legalHoldUntilUtc).getTime();
  if (!Number.isFinite(until) || until <= now.getTime()) return 'red';
  if (until - now.getTime() < 90 * DAY_MS) return 'yellow';
  return 'green';
}

/** Freshness of the latest succeeded drill from the same list as /backup/drills. */
export function drillSuccessGate(at: string | null, now = new Date()): GateColor {
  if (!at) return 'red';
  const age = now.getTime() - new Date(at).getTime();
  if (!Number.isFinite(age) || age > 30 * DAY_MS) return 'red';
  if (age > 14 * DAY_MS) return 'yellow';
  return 'green';
}

export const BACKUP_KNOWN_GAP_SCHEDULED_DRILL = 'scheduled-drill-not-run';
export const BACKUP_KNOWN_GAP_LEGAL_HOLD = 'system-legal-hold-not-configured';

/**
 * An accepted open gap turns a red gate amber. It never turns the gate green.
 * Yellow and green stay as the underlying check left them.
 */
export function downgradeGateForAcceptance(color: GateColor, acceptedOpenGap: boolean): GateColor {
  if (acceptedOpenGap && color === 'red') return 'yellow';
  return color;
}

/** Card attribute for a catalog gap. Amber is the accepted-open state. */
export function knownGapRowStatus(displayColor: string | null | undefined): 'amber' | 'red' | 'green' {
  if (displayColor === 'amber' || displayColor === 'yellow') return 'amber';
  if (displayColor === 'green') return 'green';
  return 'red';
}
