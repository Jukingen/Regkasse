/**
 * Backup & DR UI için saf eşleme fonksiyonları (i18n anahtarı / AntD rengi üretimi dashboard’da kalabilir).
 */

import type { BackupArtifactResponseDto, RestoreVerificationRunResponseDto } from '@/api/generated/model';
import type { BackupArtifactResponseDtoLifecycleState } from '@/api/generated/model/backupArtifactResponseDtoLifecycleState';

export type ConfigurationHealthUiKind = 'unknown' | 'healthy' | 'degraded' | 'unhealthy';

export type ExternalCopyVariant = 'unknown' | 'verified' | 'failed' | 'staging' | 'mixed';

/** Ant Design Tag `color` prop değerleri (RecentRunsTable / kartlar). */
export type BackupRunAntdTagColor = 'success' | 'error' | 'processing' | 'default' | 'warning';

export function normalizeHealthLevelString(level: string | undefined | null): string {
  return (level ?? '').trim().toLowerCase();
}

export function mapConfigurationHealthLevel(level: string | undefined | null): ConfigurationHealthUiKind {
  if (!level?.trim()) return 'unknown';
  const n = normalizeHealthLevelString(level);
  if (n === 'unhealthy') return 'unhealthy';
  if (n === 'degraded') return 'degraded';
  if (n === 'healthy') return 'healthy';
  return 'unknown';
}

/** Summary Statistic value color — avoid implying “all green” when readiness is capped in UI. */
export function healthStatisticValueStyle(kind: ConfigurationHealthUiKind): { color: string } | undefined {
  if (kind === 'healthy') return { color: '#52c41a' };
  if (kind === 'degraded') return { color: '#faad14' };
  if (kind === 'unhealthy') return { color: '#ff4d4f' };
  return undefined;
}

/** BackupRunResponseDtoStatus.NUMBER_3 — Succeeded; keep literal to avoid coupling tests to Orval enums. */
const BackupRunSucceededStatus = 3;

/** Matches backend Fake / ProductionStub — fallback when isSimulatedExecution is missing on DTO. */
export function isSimulatedBackupAdapterKind(adapterKind: string | null | undefined): boolean {
  const k = (adapterKind ?? '').trim();
  return k === 'Fake' || k === 'ProductionStub';
}

/**
 * Caps restore readiness API level so the summary is not “healthy” (green) when there is no real pg_dump path
 * or the latest backup success was simulated — mirrors BackupDrDashboard useMemo.
 */
export function computeEffectiveRestoreReadinessLevel(params: {
  apiLevel: string | undefined | null;
  realPostgreSqlLogicalDumpConfiguredHealth: boolean | undefined;
  realPostgreSqlLogicalDumpConfiguredRecoverability: boolean | undefined;
  latestBackupStatus: number | undefined;
  isLatestRunSimulatedExecution: boolean | undefined;
  /** From latest run DTO; used when simulated flag is undefined but adapter_kind is Fake/ProductionStub. */
  latestAdapterKind: string | null | undefined;
}): string | undefined | null {
  const {
    apiLevel,
    realPostgreSqlLogicalDumpConfiguredHealth,
    realPostgreSqlLogicalDumpConfiguredRecoverability,
    latestBackupStatus,
    isLatestRunSimulatedExecution,
    latestAdapterKind,
  } = params;

  const latestSucceededIsSimulated =
    latestBackupStatus === BackupRunSucceededStatus &&
    (isLatestRunSimulatedExecution === true || isSimulatedBackupAdapterKind(latestAdapterKind));

  const noRealProductionDump =
    realPostgreSqlLogicalDumpConfiguredHealth === false ||
    realPostgreSqlLogicalDumpConfiguredRecoverability === false ||
    latestSucceededIsSimulated;

  if (!noRealProductionDump) return apiLevel;

  const k = mapConfigurationHealthLevel(apiLevel);
  if (k === 'healthy') return 'degraded';
  return apiLevel;
}

/** Özet kart / etiket için i18n kökü: backupDr.health.* veya backupDr.summary.unknown */
export function configurationHealthSummaryI18nKey(level: string | undefined | null): string {
  const k = mapConfigurationHealthLevel(level);
  if (k === 'unknown') return 'backupDr.summary.unknown';
  if (k === 'healthy') return 'backupDr.health.healthy';
  if (k === 'degraded') return 'backupDr.health.degraded';
  return 'backupDr.health.unhealthy';
}

export function mapBackupRunStatusAntdColor(status: number | undefined): BackupRunAntdTagColor {
  if (status === undefined || status === null) return 'default';
  if (status === 3) return 'success';
  if (status === 4 || status === 5) return 'error';
  if (status === 0) return 'default';
  if (status === 1) return 'processing';
  if (status === 2) return 'warning';
  if (status === 6) return 'default';
  return 'default';
}

export function mapRestoreVerificationStatusAntdColor(status: number | undefined): BackupRunAntdTagColor {
  const s = status ?? 0;
  if (s === 2) return 'success';
  if (s === 3) return 'error';
  if (s === 0 || s === 1) return 'processing';
  return 'default';
}

/**
 * Harici kopya kartı için lifecycle özet sınıfı (metin i18n ile dashboard’da).
 */
export function mapArtifactsToExternalCopyVariant(
  artifacts: BackupArtifactResponseDto[] | undefined | null,
): ExternalCopyVariant {
  if (!artifacts?.length) return 'unknown';
  const stateList = artifacts
    .map((a) => a.lifecycleState)
    .filter((s): s is BackupArtifactResponseDtoLifecycleState => s !== undefined && s !== null);
  const states = new Set(stateList);
  if (states.has(3)) {
    if (states.size === 1) return 'failed';
    return 'mixed';
  }
  if (states.has(2) && stateList.every((s) => s === 2 || s === 1)) return 'verified';
  if (stateList.length > 0 && stateList.every((s) => s === 0 || s === 1)) return 'staging';
  return 'mixed';
}

export function externalCopyVariantToI18nKey(variant: ExternalCopyVariant): string {
  switch (variant) {
    case 'verified':
      return 'backupDr.externalCopy.verified';
    case 'failed':
      return 'backupDr.externalCopy.failed';
    case 'staging':
      return 'backupDr.externalCopy.stagingOnly';
    case 'mixed':
      return 'backupDr.externalCopy.mixed';
    default:
      return 'backupDr.externalCopy.unknown';
  }
}

export type RestoreDumpInspection = 'ok' | 'fail' | 'unknown';

export type RestoreAttemptPhase = 'ok' | 'fail' | 'not_run' | 'unknown';

export type RestoreFiscalPhase = 'ok' | 'fail' | 'skipped' | 'unknown';

export type RestoreIntegrityPhase = 'ok' | 'fail' | 'unknown';

export interface RestoreVerificationPhaseMap {
  dumpInspection: RestoreDumpInspection;
  restoreAttempt: RestoreAttemptPhase;
  fiscalSql: RestoreFiscalPhase;
  integrity: RestoreIntegrityPhase;
}

export function mapDumpInspectionTriState(
  rr: RestoreVerificationRunResponseDto | undefined | null,
): boolean | undefined {
  if (!rr) return undefined;
  if (rr.dumpInspectionPassed !== undefined && rr.dumpInspectionPassed !== null) return rr.dumpInspectionPassed;
  if (rr.pgRestoreListPassed !== undefined && rr.pgRestoreListPassed !== null) return rr.pgRestoreListPassed;
  return undefined;
}

export function mapRestoreVerificationPhases(
  rr: RestoreVerificationRunResponseDto | undefined | null,
): RestoreVerificationPhaseMap {
  if (!rr) {
    return {
      dumpInspection: 'unknown',
      restoreAttempt: 'unknown',
      fiscalSql: 'unknown',
      integrity: 'unknown',
    };
  }

  const d = mapDumpInspectionTriState(rr);
  const dumpInspection: RestoreDumpInspection = d === true ? 'ok' : d === false ? 'fail' : 'unknown';

  let restoreAttempt: RestoreAttemptPhase = 'unknown';
  if (!rr.restoreAttemptExecuted) restoreAttempt = 'not_run';
  else if (rr.restoreAttemptPassed === true) restoreAttempt = 'ok';
  else if (rr.restoreAttemptPassed === false) restoreAttempt = 'fail';

  let fiscalSql: RestoreFiscalPhase = 'unknown';
  if (rr.fiscalSqlSkipped) fiscalSql = 'skipped';
  else if (rr.fiscalSqlPassed === true) fiscalSql = 'ok';
  else if (rr.fiscalSqlPassed === false) fiscalSql = 'fail';

  let integrity: RestoreIntegrityPhase = 'unknown';
  if (rr.integrityChecksPassed === true) integrity = 'ok';
  else if (rr.integrityChecksPassed === false) integrity = 'fail';

  return { dumpInspection, restoreAttempt, fiscalSql, integrity };
}
