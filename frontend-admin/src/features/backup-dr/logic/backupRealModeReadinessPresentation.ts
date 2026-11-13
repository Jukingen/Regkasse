/**
 * Real (PgDump) önkoşul tanılarını kategorize eder; engelleyici vs danışmanlık ve i18n başlıkları.
 */
import type { BackupExecutionModeResponseDto } from '@/features/backup-dr/logic/backupExecutionModeApi';

export type HypotheticalPgDumpHealthLevel = 'Healthy' | 'Degraded' | 'Unhealthy' | '';

export type RealModeIssueCategory =
  | 'staging'
  | 'connection'
  | 'pgDump'
  | 'pgRestore'
  | 'archive'
  | 'policy'
  | 'environment'
  | 'schedule'
  | 'orchestrator'
  | 'other';

export type RealModeIssueTier = 'blocking' | 'advisory';

export interface PresentedRealModeDiagnostic {
  code: string;
  category: RealModeIssueCategory;
  tier: RealModeIssueTier;
  title: string;
  action: string;
  configKeys: string[];
  serverMessage: string;
}

/** Sunucu BackupConfigurationDiagnosticCodes ile hizalı sabit kod → UI dil anahtarı soneki. */
const codeToSlug: Record<string, string> = {
  BACKUP_SETUP_PG_DUMP_STAGING_ROOT_MISSING: 'pgDumpStagingRootMissing',
  BACKUP_SETUP_PG_DUMP_STAGING_ROOT_NOT_ABSOLUTE_NON_DEV: 'pgDumpStagingRootNotAbsolute',
  BACKUP_SETUP_PG_DUMP_VERIFY_ON_DISK_REQUIRED_NON_DEV: 'pgDumpVerifyOnDiskRequired',
  BACKUP_SETUP_PG_DUMP_EXTERNAL_ARCHIVE_REQUIRED_NON_DEV: 'pgDumpExternalArchiveRequired',
  BACKUP_SETUP_DEV_EXTERNAL_ARCHIVE_NOT_CONFIGURED: 'devExternalArchiveNotSet',
  BACKUP_SETUP_EXTERNAL_ARCHIVE_ROOT_NOT_ABSOLUTE: 'externalArchiveRootNotAbsolute',
  BACKUP_SETUP_EXTERNAL_ARCHIVE_IMMUTABILITY_ATTESTATION_MISMATCH:
    'externalArchiveImmutabilityMismatch',
  BACKUP_SETUP_EXTERNAL_ARCHIVE_OPERATOR_DISPOSITION_MISSING_NON_DEV:
    'externalArchiveOperatorDispositionMissing',
  BACKUP_SETUP_EXTERNAL_ARCHIVE_IMMUTABLE_ATTESTATION_ONLY:
    'externalArchiveImmutableAttestationOnly',
  BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_MISSING: 'pgDumpConnectionStringMissing',
  BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_INCOMPLETE: 'pgDumpConnectionStringIncomplete',
  BACKUP_SETUP_PG_DUMP_CONNECTION_STRING_INVALID: 'pgDumpConnectionStringInvalid',
  BACKUP_SETUP_DEV_PG_DUMP_CLIENT_MISSING_OR_BROKEN: 'devPgDumpClientMissing',
  BACKUP_SETUP_DEV_PG_RESTORE_CLIENT_MISSING_OR_BROKEN: 'devPgRestoreClientMissing',
  BACKUP_SETUP_FAKE_ADAPTER_FORBIDDEN_NON_DEV: 'fakeAdapterForbiddenProduction',
  BACKUP_SETUP_FAKE_ADAPTER_ACKNOWLEDGED_NON_DEV: 'fakeAdapterAcknowledgedProduction',
  BACKUP_SETUP_PRODUCTION_STUB_FORBIDDEN_NON_DEV: 'productionStubForbiddenProduction',
  BACKUP_SETUP_PRODUCTION_STUB_ACKNOWLEDGED_NON_DEV: 'productionStubAcknowledgedProduction',
  BACKUP_SETUP_ORCHESTRATOR_POLLING_INTERVAL_TOO_SHORT: 'orchestratorPollingTooShort',
  BACKUP_SETUP_ORCHESTRATOR_POLLING_INTERVAL_TOO_LONG: 'orchestratorPollingTooLong',
  BACKUP_SETUP_WORKER_DISABLED: 'workerDisabled',
  BACKUP_SETUP_ORCHESTRATOR_DISTRIBUTED_LOCK_DISABLED_NON_DEV: 'orchestratorLockDisabledNonDev',
  BACKUP_SETUP_DEV_FORCE_VERIFICATION_FAILURE_NON_DEV: 'developmentForceVerificationFailureNonDev',
  BACKUP_SETUP_RETENTION_EXECUTION_PLANNED_NOT_IMPLEMENTED: 'retentionExecutionPlanned',
  BACKUP_SETUP_SCHEDULED_BACKUP_CRON_MISSING: 'scheduledBackupCronMissing',
  BACKUP_SETUP_SCHEDULED_BACKUP_CRON_INVALID: 'scheduledBackupCronInvalid',
};

const slugToCategory: Record<string, RealModeIssueCategory> = {
  pgDumpStagingRootMissing: 'staging',
  pgDumpStagingRootNotAbsolute: 'staging',
  pgDumpVerifyOnDiskRequired: 'archive',
  pgDumpExternalArchiveRequired: 'archive',
  devExternalArchiveNotSet: 'archive',
  externalArchiveRootNotAbsolute: 'archive',
  externalArchiveImmutabilityMismatch: 'policy',
  externalArchiveOperatorDispositionMissing: 'policy',
  externalArchiveImmutableAttestationOnly: 'archive',
  pgDumpConnectionStringMissing: 'connection',
  pgDumpConnectionStringIncomplete: 'connection',
  pgDumpConnectionStringInvalid: 'connection',
  devPgDumpClientMissing: 'pgDump',
  devPgRestoreClientMissing: 'pgRestore',
  fakeAdapterForbiddenProduction: 'policy',
  fakeAdapterAcknowledgedProduction: 'policy',
  productionStubForbiddenProduction: 'policy',
  productionStubAcknowledgedProduction: 'policy',
  orchestratorPollingTooShort: 'orchestrator',
  orchestratorPollingTooLong: 'orchestrator',
  workerDisabled: 'orchestrator',
  orchestratorLockDisabledNonDev: 'orchestrator',
  developmentForceVerificationFailureNonDev: 'environment',
  retentionExecutionPlanned: 'other',
  scheduledBackupCronMissing: 'schedule',
  scheduledBackupCronInvalid: 'schedule',
};

const categorySortOrder: RealModeIssueCategory[] = [
  'staging',
  'connection',
  'pgDump',
  'pgRestore',
  'archive',
  'policy',
  'environment',
  'schedule',
  'orchestrator',
  'other',
];

export function parseHypotheticalPgDumpHealthLevel(
  raw: string | undefined | null
): HypotheticalPgDumpHealthLevel {
  const s = (raw ?? '').trim();
  if (s === 'Healthy' || s === 'Degraded' || s === 'Unhealthy') return s;
  return '';
}

export function isRealModeSelectableNow(
  d: BackupExecutionModeResponseDto | null | undefined
): boolean {
  if (!d?.selectableModes?.length) return false;
  const row = d.selectableModes.find((x) => (x.userFacingMode ?? '').trim() === 'RealPgDump');
  return row?.selectable === true;
}

function tierFromSeverity(severity: string | undefined): RealModeIssueTier {
  const s = (severity ?? '').trim().toLowerCase();
  return s === 'error' ? 'blocking' : 'advisory';
}

function translateCode(
  t: (k: string, o?: Record<string, string | number>) => string,
  slug: string,
  part: 'title' | 'action',
  fallbackTitle: string,
  fallbackAction: string
): string {
  const key = `backupDr.executionMode.realReadiness.codes.${slug}.${part}`;
  const v = t(key);
  if (v === key) return part === 'title' ? fallbackTitle : fallbackAction;
  return v;
}

export function presentRealModeDiagnostics(
  diagnostics: BackupExecutionModeResponseDto['realModeBlockingDiagnostics'],
  t: (k: string, o?: Record<string, string | number>) => string
): PresentedRealModeDiagnostic[] {
  const list = diagnostics ?? [];
  const mapped = list.map((d) => {
    const code = (d.code ?? '').trim();
    const slug = codeToSlug[code] ?? 'unknown';
    const category = slugToCategory[slug] ?? 'other';
    const tier = tierFromSeverity(d.severity);
    const keys = (d.relatedConfigurationKeys ?? []).filter(Boolean) as string[];
    const serverMessage = (d.message ?? '').trim();
    const title =
      slug === 'unknown' ? code || 'UNKNOWN' : translateCode(t, slug, 'title', code, serverMessage);
    const action =
      slug === 'unknown'
        ? serverMessage
        : translateCode(t, slug, 'action', serverMessage, serverMessage);
    return { code, category, tier, title, action, configKeys: keys, serverMessage };
  });

  return mapped.sort((a, b) => {
    const ca = categorySortOrder.indexOf(a.category);
    const cb = categorySortOrder.indexOf(b.category);
    if (ca !== cb) return ca - cb;
    if (a.tier !== b.tier) return a.tier === 'blocking' ? -1 : 1;
    return a.code.localeCompare(b.code);
  });
}

export function realReadinessSummaryCopy(
  level: HypotheticalPgDumpHealthLevel,
  realSelectable: boolean,
  t: (k: string, o?: Record<string, string | number>) => string
): { alertType: 'success' | 'warning' | 'error' | 'info'; title: string; description: string } {
  if (!level) {
    return {
      alertType: 'info',
      title: t('backupDr.executionMode.realReadiness.summary.noLevelTitle'),
      description: t('backupDr.executionMode.realReadiness.summary.noLevelBody'),
    };
  }

  if (!realSelectable) {
    return {
      alertType: 'error',
      title: t('backupDr.executionMode.realReadiness.summary.blockedTitle'),
      description: t('backupDr.executionMode.realReadiness.summary.blockedBody'),
    };
  }

  if (level === 'Healthy') {
    return {
      alertType: 'success',
      title: t('backupDr.executionMode.realReadiness.summary.readyTitle'),
      description: t('backupDr.executionMode.realReadiness.summary.readyBody'),
    };
  }

  if (level === 'Degraded') {
    return {
      alertType: 'warning',
      title: t('backupDr.executionMode.realReadiness.summary.degradedSelectableTitle'),
      description: t('backupDr.executionMode.realReadiness.summary.degradedSelectableBody'),
    };
  }

  // Unhealthy but selectable=true should not happen — treat as blocked.
  return {
    alertType: 'error',
    title: t('backupDr.executionMode.realReadiness.summary.blockedTitle'),
    description: t('backupDr.executionMode.realReadiness.summary.blockedBody'),
  };
}

export function healthLevelLabel(
  level: HypotheticalPgDumpHealthLevel,
  t: (k: string, o?: Record<string, string | number>) => string
): string {
  if (!level) return t('backupDr.executionMode.realReadiness.health.unknown');
  const key = `backupDr.executionMode.realReadiness.healthLevels.${level}`;
  const v = t(key);
  return v === key ? level : v;
}
